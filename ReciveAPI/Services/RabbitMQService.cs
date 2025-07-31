using RabbitMQ.Client;
using ReciveAPI.Services.IServices;
using System.Text;

namespace ReciveAPI.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "Processing_Result";
        private readonly ILogger<IRabbitMQService> _logger;
        public RabbitMQService(ILogger<IRabbitMQService> logger)
        {
            _logger = logger;

            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
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
            try
            {
                var body = Encoding.UTF8.GetBytes( message );
                _channel.BasicPublish
                    (
                    exchange: "",
                    routingKey: _queueName,
                    basicProperties: null,
                    body: body
                    );
                _logger.LogInformation("Sent message to RabbitMQ: {Message}", message);
            }
            catch ( Exception ex )
            {
                _logger.LogError(ex, "Error sending message to RabbitMQ");
            }
        }
    }
}
