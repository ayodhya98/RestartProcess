using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using ReciveAPI.Services.IServices;
using System.Diagnostics;

namespace ReciveAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrackingController : ControllerBase
    {
        private readonly IFileProcessingQueueServices _queueServices;
        private readonly ILogger<TrackingController> _logger;
        private readonly ActivitySource _activitySource;
        public TrackingController
            (
             IFileProcessingQueueServices queueServices,
             ILogger<TrackingController> logger
            )
        {
            _queueServices = queueServices;
            _logger = logger;
            _activitySource = new ActivitySource("TrackingController");
        }

        [HttpPost("process-file")]
        public async Task<IActionResult> ProcessFile(IFormFile file)
        {
            using var activity = _activitySource.StartActivity("POST /api/tracking/process-file");
            activity?.AddTag("http.method", "POST");
            activity?.AddTag("http.route", "/api/tracking/process-file");
            activity?.AddTag("controller.action", "ProcessFile");

            _logger.LogInformation("File upload request received. Activity={ActivityId}", activity?.Id);

            if (file == null || file.Length == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "No file uploaded");
                activity?.AddTag("validation.error", "no_file");
                activity?.AddTag("http.status_code", 400);

                _logger.LogWarning("File upload validation failed - no file provided. Activity={ActivityId}", activity?.Id);

                return BadRequest("Please upload a file");
            }

            var fileName = file.FileName;
            var fileSize = file.Length;
            var fileExtension = Path.GetExtension(fileName).ToLower();

            activity?.AddTag("file.name", fileName);
            activity?.AddTag("file.size", fileSize);
            activity?.AddTag("file.extension", fileExtension);

            _logger.LogInformation("File upload details. FileName={FileName}, FileSize={FileSize}, FileExtension={FileExtension}, Activity={ActivityId}",
                fileName, fileSize, fileExtension, activity?.Id);

            if (Path.GetExtension(file.FileName).ToLower() != ".json")
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid file type");
                activity?.AddTag("validation.error", "invalid_file_type");
                activity?.AddTag("http.status_code", 400);

                _logger.LogWarning("File upload validation failed - invalid file type. FileName={FileName}, FileExtension={FileExtension}, Activity={ActivityId}",
                    fileName, fileExtension, activity?.Id);

                return BadRequest("Only JSON files are supported");
            }

            if (file.Length > 100_000_000)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "File too large");
                activity?.AddTag("validation.error", "file_too_large");
                activity?.AddTag("http.status_code", 400);

                _logger.LogWarning("File upload validation failed - file too large. FileName={FileName}, FileSize={FileSize}, MaxSize=100000000, Activity={ActivityId}",
                    fileName, fileSize, activity?.Id);

                return BadRequest("File size exceeds 100 MB limit");
            }

            try
            {
                using var fileProcessingActivity = _activitySource.StartActivity("CreateTempFile");
                fileProcessingActivity?.AddTag("operation.type", "temp_file_creation");

                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

                fileProcessingActivity?.AddTag("temp.file.path", tempFilePath);
                if (tempFilePath.Length > 260)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "Temp file path too long");
                    activity?.AddTag("validation.error", "temp_path_too_long");
                    activity?.AddTag("temp.path.length", tempFilePath.Length);
                    activity?.AddTag("http.status_code", 500);

                    _logger.LogError("Temporary file path too long. TempPath={TempPath}, PathLength={PathLength}, Activity={ActivityId}",
                        tempFilePath, tempFilePath.Length, activity?.Id);

                    return StatusCode(500, "Temporary file path is too long");
                }

                await using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                fileProcessingActivity?.SetStatus(ActivityStatusCode.Ok);
                fileProcessingActivity?.AddTag("temp.file.created", true);

                _logger.LogInformation("Temporary file created successfully. TempFilePath={TempFilePath}, Activity={ActivityId}",
                    tempFilePath, fileProcessingActivity?.Id);

                using var queueActivity = _activitySource.StartActivity("EnqueueFile");
                queueActivity?.AddTag("operation.type", "file_enqueue");
                queueActivity?.AddTag("file.path", tempFilePath);

                _queueServices.Enqueue(tempFilePath);

                queueActivity?.SetStatus(ActivityStatusCode.Ok);
                queueActivity?.AddTag("file.enqueued", true);

                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.AddTag("http.status_code", 202);
                activity?.AddTag("processing.status", "accepted");

                _logger.LogInformation("File processing request completed successfully. FileName={FileName}, TempFilePath={TempFilePath}, FileSize={FileSize}, Activity={ActivityId}",
                    fileName, tempFilePath, fileSize, activity?.Id);

                return Accepted(new
                {
                    Message = "File is being processed",
                    FileName = file.FileName,
                    Size = file.Length
                });
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                activity?.AddTag("http.status_code", 500);
                activity?.AddTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, "Error processing file upload request. FileName={FileName}, ErrorType={ErrorType}, Activity={ActivityId}",
                    fileName, ex.GetType().Name, activity?.Id);

                return StatusCode(500, "Internal server error");
            }
        }
    }
}
