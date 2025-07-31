using BackgroundProcessWorker.Services.IServices;

namespace BackgroundProcessWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        public Worker(ILogger<Worker> logger, IRabbitMQService rabbitMQService)
        {
            _logger = logger;
            _rabbitMQService = rabbitMQService;
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        if (_logger.IsEnabled(LogLevel.Information))
        //        {
        //            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        //        }
        //        await Task.Delay(1000, stoppingToken);
        //    }
        //}

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started listening for messages...");

            _rabbitMQService.StartListening(message =>
            {
                _logger.LogInformation("Received message: {Message}", message);
            });

            return Task.CompletedTask;
        }
    }
}
