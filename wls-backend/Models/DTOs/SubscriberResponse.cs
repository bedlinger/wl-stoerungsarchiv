using wls_backend.Models.Domain;

namespace wls_backend.Models.DTOs
{
    public class SubscriberResponse
    {
        public string Token { get; set; } = null!;
        public List<LineResponse> Subscriptions { get; set; } = new();

        public static SubscriberResponse FromDomain(Subscriber subscriber)
        {
            return new SubscriberResponse
            {
                Token = subscriber.Token,
                Subscriptions = subscriber.Subscriptions
                                      .Select(sub => LineResponse.FromDomain(sub.Line))
                                      .ToList()
            };
        }
    }
}
