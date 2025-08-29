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
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(SubscriptionService subscriptionService, ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        [HttpGet("{token}")]
        public async Task<ActionResult<SubscriptionResponse>> GetSubscriptions([FromRoute] string token)
        {
            _logger.LogInformation("Attempting to get subscriptions for token.");
            try
            {
                var subscription = await _subscriptionService.GetSubscriptions(token);

                if (subscription == null)
                {
                    _logger.LogWarning("No subscription found for the provided token.");
                    return NotFound();
                }

                return Ok(subscription);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid token provided for GetSubscriptions.");
                return BadRequest(new ProblemDetails { Title = "Invalid Token", Detail = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in GetSubscriptions.");
                return StatusCode(500, new ProblemDetails { Title = "An unexpected error occurred", Detail = "The server encountered an internal error." });
            }
        }

        [HttpPut("{token}")]
        public async Task<ActionResult<SubscriptionResponse>> CreateOrUpdateSubscriptions([FromRoute] string token, [FromBody] UpdateSubscriptionsRequest request)
        {
            _logger.LogInformation("Attempting to create or update subscriptions for token.");
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
                _logger.LogError(ex, "Invalid argument during subscription creation/update.");
                return BadRequest(new ProblemDetails { Title = "Invalid Token or Request", Detail = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in CreateOrUpdateSubscriptions.");
                return StatusCode(500, new ProblemDetails { Title = "An unexpected error occurred", Detail = "The server encountered an internal error." });
            }
        }

        [HttpDelete("{token}")]
        public async Task<IActionResult> DeleteSubscription([FromRoute] string token)
        {
            _logger.LogInformation("Attempting to delete subscription for token.");
            try
            {
                await _subscriptionService.DeleteSubscription(token);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in DeleteSubscription.");
                return StatusCode(500, new ProblemDetails { Title = "An unexpected error occurred", Detail = "The server encountered an internal error." });
            }
        }
    }
}
