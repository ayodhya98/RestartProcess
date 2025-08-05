using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
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
        private readonly IGridFSBucket _gridFSBucket;

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
            _gridFSBucket = new GridFSBucket(database, new GridFSBucketOptions
            {
                BucketName = "invoices",
                ChunkSizeBytes = 255 * 1024
            });
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

            var fileInfo = new FileInfo(filePath);
            activity?.AddTag("file.size", fileInfo.Length);

            ObjectId gridFSFileId = ObjectId.Empty;
            try
            {
                using (var fileStream = File.OpenRead(filePath))
                {
                    var uploadOptions = new GridFSUploadOptions
                    {
                        Metadata = new BsonDocument
                {
                    { "filename", Path.GetFileName(filePath) },
                    { "uploadDate", DateTime.UtcNow },
                    { "contentType", "application/json" }
                }
                    };

                    using var gridFSActivity = _activitySource.StartActivity("GridFSUpload");
                    gridFSActivity?.AddTag("file.path", filePath);
                    gridFSFileId = await _gridFSBucket.UploadFromStreamAsync(
                        Path.GetFileName(filePath),
                        fileStream,
                        uploadOptions);
                    gridFSActivity?.SetStatus(ActivityStatusCode.Ok);
                    gridFSActivity?.AddTag("gridfs.file.id", gridFSFileId.ToString());

                    _logger.LogInformation("File uploaded to GridFS with ID: {GridFSFileId}", gridFSFileId);
                }

                await using (var fileStream = File.OpenRead(filePath))
                using (var streamReader = new StreamReader(fileStream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonReader.SupportMultipleContent = true;
                    jsonReader.FloatParseHandling = FloatParseHandling.Decimal;

                    var records = new List<TrackingRecord>();
                    while (await jsonReader.ReadAsync())
                    {
                        try
                        {
                            if (jsonReader.TokenType == JsonToken.StartObject)
                            {
                                var obj = await JObject.LoadAsync(jsonReader);
                                using var parseActivity = _activitySource.StartActivity("ParseJsonObject");
                                parseActivity?.AddTag("file.path", filePath);

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
                                                JsonObject = BsonDocument.Parse(obj.ToString(Formatting.None)),
                                                FileName = Path.GetFileName(filePath),
                                                ProcessedAt = DateTime.UtcNow,
                                                GridFSFileId = gridFSFileId.ToString()
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
                                            JsonObject = BsonDocument.Parse(obj.ToString(Formatting.None)),
                                            FileName = Path.GetFileName(filePath),
                                            ProcessedAt = DateTime.UtcNow,
                                            GridFSFileId = gridFSFileId.ToString()
                                        });
                                    }
                                }
                                parseActivity?.SetStatus(ActivityStatusCode.Ok);
                                _logger.LogInformation("Parsed JSON object with {RecordCount} tracking records from {FilePath}", records.Count, filePath);
                            }
                        }
                        catch (JsonReaderException jex)
                        {
                            _logger.LogWarning(jex, "Skipping malformed JSON object in {FilePath}", filePath);
                            continue;
                        }
                    }

                    if (records.Any())
                    {
                        using var dbActivity = _activitySource.StartActivity("MongoDBInsert");
                        dbActivity?.AddTag("db.operation", "InsertMany");
                        dbActivity?.AddTag("db.collection", "TrackingRecords");
                        dbActivity?.AddTag("record.count", records.Count);
                        await _trackingCollection.InsertManyAsync(records);
                        dbActivity?.SetStatus(ActivityStatusCode.Ok);
                        _logger.LogInformation("Inserted {RecordCount} records into MongoDB for {FilePath}", records.Count, filePath);
                    }
                    else
                    {
                        _logger.LogWarning("No tracking records found in {FilePath}", filePath);
                    }
                }

                _logger.LogInformation("Completed processing file: {FilePath}", filePath);
                //try
                //{
                //    File.Delete(filePath);
                //    _logger.LogInformation("Deleted temporary file: {FilePath}", filePath);
                //}
                //catch (Exception ex)
                //{
                //    _logger.LogWarning(ex, "Failed to delete temporary file {FilePath}", filePath);
                //}

                _rabbitMQService.SendMessage($"Processing completed successfully for file: {filePath}");
            }
            catch (MongoGridFSException gfex)
            {
                _logger.LogError(gfex, "GridFS error processing file {FilePath}", filePath);
                activity?.SetStatus(ActivityStatusCode.Error, gfex.Message);
                throw;
            }
            catch (PathTooLongException pex)
            {
                _logger.LogError(pex, "Path too long for file {FilePath}", filePath);
                activity?.SetStatus(ActivityStatusCode.Error, pex.Message);
                throw new InvalidOperationException("The file path is too long.", pex);
            }
            catch (IOException ioex)
            {
                _logger.LogError(ioex, "IO error processing file {FilePath}", filePath);
                activity?.SetStatus(ActivityStatusCode.Error, ioex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}