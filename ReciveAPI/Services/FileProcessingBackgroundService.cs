using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReciveAPI.Services.IServices;
using System.Text.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

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


        private async Task ProcessFileAsync(string filePath)
        {
            _logger.LogInformation("Starting streaming processing of: {FilePath}", filePath);

            string outputFileName = $"TrackingNumbers_{DateTime.Now:yyyyMMddHHmmss}.txt";

            try
            {
                await using (var outputWriter = new StreamWriter(outputFileName))
                await using (var fileStream = File.OpenRead(filePath))
                using (var streamReader = new StreamReader(fileStream))
                using (var jsonReader = new JsonTextReader(streamReader))


                {
                    jsonReader.SupportMultipleContent = true;
                    jsonReader.FloatParseHandling = FloatParseHandling.Decimal;

                    var serializer = new JsonSerializer();

                    while (await jsonReader.ReadAsync())
                    {
                        try
                        {
                            if (jsonReader.TokenType == JsonToken.StartObject)
                            {
                                var obj = await JObject.LoadAsync(jsonReader);

                                if (obj["DocumentLines"] is JArray documentLines)
                                {
                                    foreach (var line in documentLines)
                                    {
                                        var trackingNo = line["U_TrackingNo"]?.ToString();
                                        if (!string.IsNullOrEmpty(trackingNo))
                                        {
                                            await outputWriter.WriteLineAsync(trackingNo);
                                        }
                                    }
                                }
                                else
                                {
                                    var trackingNo = obj["U_TrackingNo"]?.ToString();
                                    if (!string.IsNullOrEmpty(trackingNo))
                                    {
                                        await outputWriter.WriteLineAsync(trackingNo);
                                    }
                                }
                            }
                        }
                        catch (JsonReaderException jex)
                        {
                            _logger.LogWarning(jex, "Skipping malformed JSON object");
                            continue;
                        }
                    }
                }

                _logger.LogInformation("Completed processing. Output: {OutputFile}", outputFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                throw;
            }
        }

    }
}
