using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Trace;
using ReciveAPI.Models;
using ReciveAPI.Services.IServices;
using System.Diagnostics;

namespace ReciveAPI.Services
{
    public class FileProcessingBackgroundService : BackgroundService
    {
        private readonly IFileProcessingQueueServices _queueServices;
        private readonly ILogger<FileProcessingBackgroundService> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IMongoCollection<TrackingRecord> _trackingCollection;
        private readonly ActivitySource _activitySource;

        public FileProcessingBackgroundService(
            IFileProcessingQueueServices queueServices,
            ILogger<FileProcessingBackgroundService> logger,
            IRabbitMQService rabbitMQService,
            IConfiguration configuration)
        {
            _queueServices = queueServices;
            _logger = logger;
            _rabbitMQService = rabbitMQService;

            _activitySource = new ActivitySource("FileProcessingBackgroundService");
            var mongoSettings = configuration.GetSection("MongoDB");
            var client = new MongoClient(mongoSettings["ConnectionString"]);
            var database = client.GetDatabase(mongoSettings["DatabaseName"]);
            _trackingCollection = database.GetCollection<TrackingRecord>(mongoSettings["CollectionName"]);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var activity = _activitySource.StartActivity("BackgroundServiceExecution");
            _logger.LogInformation("File Processing Background Service is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_queueServices.TryDequeue(out var filePath))
                {
                    var ProcessActivity = _activitySource.StartActivity("processFile");
                    ProcessActivity?.AddTag("file.path", filePath);

                    try
                    {
                        await ProcessFileAsync(filePath);
                        _rabbitMQService.SendMessage("Processing completed successfully for file.");
                    }
                    catch (Exception ex)
                    {
                        ProcessActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        ProcessActivity?.RecordException(ex);

                        _logger.LogError(ex, "Error processing file at {FilePath}", filePath);
                        _rabbitMQService.SendMessage($"Processing failed: {ex.Message}");
                    }
                }
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
            var activity = _activitySource.StartActivity("ProcessFileAsync");
            activity?.AddTag("file.path", filePath);

            _logger.LogInformation("Starting streaming processing of: {FilePath}", filePath);

            if (filePath.Length > 260)
            {
                var ex = new InvalidOperationException("File path is too long. Ensure the path is within 260 characters.");

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw ex;
            }

            var fileInfo = new FileInfo(filePath);
            activity?.AddTag("file.size", fileInfo.Length);

            if (fileInfo.Length > 100_000_000)
            {
                var ex = new InvalidOperationException("File is too large to process.");

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw ex;
            }

            try
            {
                await using (var fileStream = File.OpenRead(filePath))
                using (var streamReader = new StreamReader(fileStream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonReader.SupportMultipleContent = true;
                    jsonReader.FloatParseHandling = FloatParseHandling.Decimal;

                    while (await jsonReader.ReadAsync())
                    {
                        try
                        {
                            if (jsonReader.TokenType == JsonToken.StartObject)
                            {
                                var obj = await JObject.LoadAsync(jsonReader);
                                var records = new List<TrackingRecord>();

                                if (obj["DocumentLines"] is JArray documentLines)
                                {
                                    foreach (var line in documentLines)
                                    {
                                        var trackingNo = line["U_TrackingNo"]?.ToString();
                                        if (!string.IsNullOrEmpty(trackingNo))
                                        {
                                            records.Add(new TrackingRecord
                                            {
                                                TrackingNumber = trackingNo,
                                                JsonObject = obj.ToObject<BsonDocument>(),
                                                FileName = Path.GetFileName(filePath),
                                                ProcessedAt = DateTime.UtcNow
                                            });
                                        }
                                    }
                                }
                                else
                                {
                                    var trackingNo = obj["U_TrackingNo"]?.ToString();
                                    if (!string.IsNullOrEmpty(trackingNo))
                                    {
                                        records.Add(new TrackingRecord
                                        {
                                            TrackingNumber = trackingNo,
                                            JsonObject = obj.ToObject<BsonDocument>(),
                                            FileName = Path.GetFileName(filePath),
                                            ProcessedAt = DateTime.UtcNow
                                        });
                                    }
                                }

                                if (records.Any())
                                {
                                    await _trackingCollection.InsertManyAsync(records);
                                }
                            }
                        }
                        catch (JsonReaderException jex)
                        {
                            activity?.RecordException(jex);
                            _logger.LogWarning(jex, "Skipping malformed JSON object");
                            continue;
                        }

                    }
                }

                _logger.LogInformation("Completed processing file: {FilePath}", filePath);
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
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
            }
        }
    }
}