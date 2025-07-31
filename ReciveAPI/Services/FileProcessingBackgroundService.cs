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
                if (_queueServices.TryDequeue(out var filePath))
                {
                    try
                    {
                        await ProcessFileAsync(filePath);
                        _rabbitMQService.SendMessage("Processing completed successfully for file.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file at {FilePath}", filePath);
                        _rabbitMQService.SendMessage($"Processing failed: {ex.Message}");
                    }
                }
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
            _logger.LogInformation("Starting streaming processing of: {FilePath}", filePath);
            if (filePath.Length > 260)
            {
                throw new InvalidOperationException("File path is too long. Ensure the path is within 260 characters.");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 100_000_000)
            {
                throw new InvalidOperationException("File is too large to process.");
            }

            string outputFileName = Path.Combine(Path.GetTempPath(), $"Tracking_{Guid.NewGuid()}.txt");
            if (outputFileName.Length > 260)
            {
                throw new InvalidOperationException("Output file path is too long.");
            }

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

                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {FilePath}", filePath);
                }
            }
            catch (PathTooLongException pex)
            {
                _logger.LogError(pex, "Path too long for file {FilePath}", filePath);
                throw new InvalidOperationException("The file path is too long.", pex);
            }
            catch (IOException ioex)
            {
                _logger.LogError(ioex, "IO error processing file {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                throw;
            }
        }
    }
}
