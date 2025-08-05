using BackgroundProcessWorker.Services.IServices;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BackgroundProcessWorker.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "processing_results";
        private readonly ILogger<IRabbitMQService> _logger;
        private readonly ActivitySource _activitySource;
        private readonly Meter _meter;
        private readonly Counter<long> _messagesReceivedCounter;

        public RabbitMQService(ILogger<IRabbitMQService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _activitySource = new ActivitySource("BackgroundProcessWorker.RabbitMQ");
            _meter = new Meter("BackgroundProcessWorker.RabbitMQ");
            _messagesReceivedCounter = _meter.CreateCounter<long>("rabbitmq.messages.received");

            try
            {
                using var activity = _activitySource.StartActivity("RabbitMQService Initialization");

                var factory = new ConnectionFactory()
                {
                    HostName = configuration["RabbitMQ:HostName"],
                    UserName = configuration["RabbitMQ:Username"],
                    Password = configuration["RabbitMQ:Password"],
                    Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672")
                };

                activity?.AddTag("rabbitmq.host", factory.HostName);
                activity?.AddTag("rabbitmq.port", factory.Port);

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: _queueName,
                                    durable: false,
                                    exclusive: false,
                                    autoDelete: false,
                                    arguments: null);

                activity?.AddTag("rabbitmq.queue", _queueName);
                _logger.LogInformation("RabbitMQ service initialized successfully. Connected to {HostName}:{Port}", factory.HostName, factory.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ service");
                throw;
            }
        }

        public void StartListening(Action<string> messageHandler)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                using var activity = _activitySource.StartActivity("Process RabbitMQ Message");
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    activity?.AddTag("message.length", body.Length);
                    activity?.AddTag("rabbitmq.queue", _queueName);
                    activity?.AddTag("rabbitmq.delivery_tag", ea.DeliveryTag);

                    _messagesReceivedCounter.Add(1, new KeyValuePair<string, object?>("queue", _queueName));
                    _logger.LogDebug("Message received from queue {QueueName}, length: {MessageLength}", _queueName, body.Length);

                    messageHandler(message);

                    _logger.LogDebug("Message processed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RabbitMQ message");
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    throw;
                }
            };

            _channel.BasicConsume(queue: _queueName,
                                 autoAck: true,
                                 consumer: consumer);

            _logger.LogInformation("Started listening to RabbitMQ queue: {QueueName}", _queueName);
        }
    }
}