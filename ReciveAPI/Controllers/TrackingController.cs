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

        [HttpPost("process")]
        public IActionResult ProcessFile([FromBody] string jsonContent)
        {
            _logger.LogInformation("Received file for processing");

            _queueServices.Enqueue(jsonContent);

            return Accepted();
        }
    }
}
