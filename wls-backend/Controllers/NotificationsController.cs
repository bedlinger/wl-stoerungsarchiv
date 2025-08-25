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
        public async Task<ActionResult<SubscriberResponse>> GetSubscriber([FromRoute] string token)
        {
            var subscriberDto = await _notificationsService.GetSubscriber(token);

            if (subscriberDto == null)
            {
                return NotFound();
            }

            return Ok(subscriberDto);
        }

        [HttpPut("{token}")]
        public async Task<IActionResult> CreateOrUpdateSubscription([FromRoute] String token, [FromBody] UpdateSubscriptionsRequest request)
        {
            try
            {
                var subscribeRequest = new SubscribeRequest
                {
                    Token = token,
                    Lines = request.Lines
                };

                var result = await _notificationsService.AddSubscriber(subscribeRequest);

                if (result.WasCreated)
                {
                    var locationUrl = Url.Action(nameof(GetSubscriber), new { token = result.Subscriber.Token });
                    return Created(locationUrl, result.Subscriber);
                }
                else
                {
                    return Ok(result.Subscriber);
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
        public async Task<IActionResult> Unsubscribe([FromRoute] String token)
        {
            try
            {
                await _notificationsService.RemoveSubscriber(token);
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}
