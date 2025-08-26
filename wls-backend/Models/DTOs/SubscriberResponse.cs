using wls_backend.Models.Domain;

namespace wls_backend.Models.DTOs
{
    public class SubscriptionResponse
    {
        public string Token { get; set; } = null!;
        public List<LineResponse> SubscribedLines { get; set; } = new();

        public static SubscriptionResponse FromDomain(Device device)
        {
            return new SubscriptionResponse
            {
                Token = device.Token,
                SubscribedLines = device.Subscriptions
                                      .Select(sub => LineResponse.FromDomain(sub.Line))
                                      .ToList()
            };
        }
    }
}
