using BackgroundProcessWorker.Services.IServices;
using System.Diagnostics;

namespace BackgroundProcessWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private static readonly ActivitySource _testActivitySource = new("FileProcessingBackgroundService");
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
            _logger.LogInformation("Background Worker service starting up at {Time}", DateTimeOffset.Now);
            _logger.LogInformation("Worker started listening for messages...");

            using var activity = _testActivitySource.StartActivity("TestActivity");
            activity?.SetTag("custom.tag", "hello from background worker");

            _logger.LogInformation("Activity started with ID: {ActivityId}", activity?.Id);

            _rabbitMQService.StartListening(message =>
            {
                _logger.LogInformation("Received message: {Message}", message);
                _logger.LogDebug("Processing message at {Time}", DateTimeOffset.Now);
            });

            _logger.LogInformation("Background Worker service initialized successfully");

            return Task.CompletedTask;
        }
    }
}
