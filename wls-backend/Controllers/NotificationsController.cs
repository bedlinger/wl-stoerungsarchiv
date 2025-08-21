using Microsoft.AspNetCore.Mvc;
using wls_backend.Services;
using wls_backend.Models.DTOs;

namespace wls_backend.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationsService _notificationsService;
        public NotificationsController(NotificationsService notificationsService)
        {
            _notificationsService = notificationsService;
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest subscribeRequest)
        {
            try
            {
                await _notificationsService.AddSubscriber(subscribeRequest);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] String token)
        {
            try
            {
                await _notificationsService.RemoveSubscriber(token);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
