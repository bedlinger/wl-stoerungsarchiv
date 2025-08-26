using Microsoft.AspNetCore.Mvc;
using wls_backend.Services;
using wls_backend.Models.DTOs;

namespace wls_backend.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SubscriptionsController : ControllerBase
    {
        private readonly SubscriptionService _subscriptionService;
        public SubscriptionsController(SubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet("{token}")]
        public async Task<ActionResult<SubscriptionResponse>> GetSubscriptions([FromRoute] string token)
        {
            var subscription = await _subscriptionService.GetSubscriptions(token);

            if (subscription == null)
            {
                return NotFound();
            }

            return Ok(subscription);
        }

        [HttpPut("{token}")]
        public async Task<IActionResult> CreateOrUpdateSubscriptions([FromRoute] String token, [FromBody] UpdateSubscriptionsRequest request)
        {
            try
            {
                var subscriptionRequest = new CreateOrUpdateSubscriptionRequest
                {
                    Token = token,
                    Lines = request.Lines
                };

                var (wasCreated, subscription) = await _subscriptionService.CreateOrUpdateSubscriptions(subscriptionRequest);

                if (wasCreated)
                {
                    var locationUrl = Url.Action(nameof(GetSubscriptions), new { token = subscription.Token });
                    return Created(locationUrl, subscription);
                }
                else
                {
                    return Ok(subscription);
                }
            }
            catch (ArgumentException ex)
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
                await _subscriptionService.DeleteSubscription(token);
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}
