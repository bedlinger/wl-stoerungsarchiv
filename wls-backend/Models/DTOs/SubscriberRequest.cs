namespace wls_backend.Models.DTOs
{
    public class CreateOrUpdateSubscriptionRequest
    {
        public string Token { get; set; } = "";
        public string Lines { get; set; } = "";
    }

    public class UpdateSubscriptionsRequest
    {
        public string Lines { get; set; } = "";
    }
}
