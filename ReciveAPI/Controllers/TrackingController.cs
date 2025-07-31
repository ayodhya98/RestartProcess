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

            try
            {
                // Read the file content
                using var reader = new StreamReader(file.OpenReadStream());
                var fileContent = await reader.ReadToEndAsync();

                // Add to queue for background processing
                _queueServices.Enqueue(fileContent);

                _logger.LogInformation("File {FileName} queued for processing", file.FileName);
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
