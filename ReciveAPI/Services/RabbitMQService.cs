using OpenTelemetry.Trace;
using RabbitMQ.Client;
using ReciveAPI.Services.IServices;
using System.Diagnostics;
using System.Text;

namespace ReciveAPI.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "Processing_Result";
        private readonly ILogger<IRabbitMQService> _logger;
        private readonly ActivitySource _activitySource;
        public RabbitMQService(ILogger<IRabbitMQService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _activitySource = new ActivitySource("RabbitMQService");

            var factory = new ConnectionFactory()
            {
                HostName = configuration["RabbitMQ:HostName"],
                UserName = configuration["RabbitMQ:Username"],
                Password = configuration["RabbitMQ:Password"],
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672")
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare
                (
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
                );
        }

        public void SendMessage(string message)
        {
            var activity = _activitySource.StartActivity("RabbitMQ.SendMessage");
            activity?.AddTag("messaging.system", "rabbitmq");
            activity?.AddTag("messaging.destination", _queueName);
            activity?.AddTag("messaging.operation", "publish");
            activity?.AddTag("message.size", Encoding.UTF8.GetByteCount(message));

            try
            {
                var body = Encoding.UTF8.GetBytes(message);
                _channel.BasicPublish
                    (
                    exchange: "",
                    routingKey: _queueName,
                    basicProperties: null,
                    body: body
                    );

                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.AddTag("message.sent", true);

                _logger.LogInformation("Message sent to RabbitMQ successfully. Queue={QueueName}, MessageSize={MessageSize}, Activity={ActivityId}",
                    _queueName, body.Length, activity?.Id);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                activity?.AddTag("message.sent", false);

                _logger.LogError(ex, "Error sending message to RabbitMQ. Queue={QueueName}, ErrorType={ErrorType}, Activity={ActivityId}",
                  _queueName, ex.GetType().Name, activity?.Id);
                throw;
            }
        }
        public void Dispose()
        {
            _logger.LogInformation("Disposing RabbitMQ service. QueueName={QueueName}", _queueName);

            _channel?.Close();
            _connection?.Close();
            _activitySource?.Dispose();

            _logger.LogInformation("RabbitMQ service disposed successfully");
        }
    }
}
