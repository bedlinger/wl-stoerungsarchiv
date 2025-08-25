using wls_backend.Models.Domain;

namespace wls_backend.Models.DTOs
{
    public class SubscriptionResponse
    {
        public int Id { get; set; }
        public LineResponse Line { get; set; } = null!;

        public static SubscriptionResponse FromDomain(Subscription subscription)
        {
            return new SubscriptionResponse
            {
                Id = subscription.SubscriberId,
                Line = LineResponse.FromDomain(subscription.Line)
            };
        }
    }

    public class SubscriberResponse
    {
        public string Token { get; set; } = null!;
        public List<SubscriptionResponse> Subscriptions { get; set; } = new();

        public static SubscriberResponse FromDomain(Subscriber subscriber)
        {
            return new SubscriberResponse
            {
                Token = subscriber.Token,
                Subscriptions = subscriber.Subscriptions
                                          .Select(SubscriptionResponse.FromDomain)
                                          .ToList()
            };
        }
    }

    public class SubscribeResult
    {
        required public bool WasCreated { get; set; }
        required public SubscriberResponse Subscriber { get; set; }

    }
}
