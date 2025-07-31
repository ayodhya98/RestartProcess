using ReciveAPI.Services.IServices;
using System.Text.Json;

namespace ReciveAPI.Services
{
    public class FileProcessingBackgroundService : BackgroundService
    {
        private readonly IFileProcessingQueueServices _queueServices;
        private readonly ILogger<FileProcessingBackgroundService> _logger;
        private readonly IRabbitMQService _rabbitMQService;

        public FileProcessingBackgroundService
        (
        IFileProcessingQueueServices queueServices,
        ILogger<FileProcessingBackgroundService> logger,
        IRabbitMQService rabbitMQService
        )
        {
            _queueServices = queueServices;
            _logger = logger;
            _rabbitMQService = rabbitMQService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("File Processing Background Service is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_queueServices.TryDequeue(out var fileContent))
                {
                    try
                    {
                        await ProcessFileAsync(fileContent);
                        _rabbitMQService.SendMessage("Processing completed successfully for file.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file");
                        _rabbitMQService.SendMessage($"Processing failed: {ex.Message}");
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessFileAsync(string jsonContent)
        {
            _logger.LogInformation("Starting file processing...");

            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            string fileName = $"TrackingNumbers_{DateTime.Now:yyyyMMddHHmmss}.txt";

            await using (StreamWriter writer = new StreamWriter(fileName))
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.TryGetProperty("U_TrackingNo", out var trackingNo))
                    {
                        await writer.WriteLineAsync(trackingNo.GetString());
                    }
                }
            }

            _logger.LogInformation($"Completed processing. Tracking numbers written to {fileName}");
        }
    }
}
