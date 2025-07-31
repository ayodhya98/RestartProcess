using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReciveAPI.Services.IServices;

namespace ReciveAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrackingController : ControllerBase
    {
        private readonly IFileProcessingQueueServices _queueServices;
        private readonly ILogger<TrackingController> _logger;
        public TrackingController
            (
             IFileProcessingQueueServices queueServices,
             ILogger<TrackingController> logger
            )
        {
            _queueServices = queueServices;
            _logger = logger;
        }

        [HttpPost("process-file")]
        public async Task<IActionResult> ProcessFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file uploaded");
                return BadRequest("Please upload a file");
            }

            if (Path.GetExtension(file.FileName).ToLower() != ".json")
            {
                _logger.LogWarning("Invalid file type uploaded: {FileName}", file.FileName);
                return BadRequest("Only JSON files are supported");
            }

            if (file.Length > 100_000_000)
            {
                _logger.LogWarning("File too large: {FileSize} bytes", file.Length);
                return BadRequest("File size exceeds 100 MB limit");
            }

            try
            {
                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
                if (tempFilePath.Length > 260)
                {
                    _logger.LogError("Generated temporary file path too long: {Path}", tempFilePath);
                    return StatusCode(500, "Temporary file path is too long");
                }

                await using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                _queueServices.Enqueue(tempFilePath);
                _logger.LogInformation("File {FileName} queued for processing at {Path}", file.FileName, tempFilePath);

                return Accepted(new
                {
                    Message = "File is being processed",
                    FileName = file.FileName,
                    Size = file.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file upload");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
