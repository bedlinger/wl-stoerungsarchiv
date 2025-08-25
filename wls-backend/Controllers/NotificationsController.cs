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

        [HttpGet("{token}")]
        public async Task<ActionResult<SubscriberResponse>> GetSubscription([FromRoute] string token)
        {
            var subscriber = await _notificationsService.GetSubscription(token);

            if (subscriber == null)
            {
                return NotFound();
            }

            return Ok(subscriber);
        }

        [HttpPut("{token}")]
        public async Task<IActionResult> CreateOrUpdateSubscription([FromRoute] String token, [FromBody] UpdateSubscriptionsRequest request)
        {
            try
            {
                var subscribeRequest = new SubscriberRequest
                {
                    Token = token,
                    Lines = request.Lines
                };

                var (wasCreated, subscriber) = await _notificationsService.CreateOrUpdateSubscription(subscribeRequest);

                if (wasCreated)
                {
                    var locationUrl = Url.Action(nameof(GetSubscription), new { token = subscriber.Token });
                    return Created(locationUrl, subscriber);
                }
                else
                {
                    return Ok(subscriber);
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        [HttpDelete("{token}")]
        public async Task<IActionResult> DeleteSubscription([FromRoute] String token)
        {
            try
            {
                await _notificationsService.DeleteSubscription(token);
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}
