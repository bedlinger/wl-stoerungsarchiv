namespace wls_backend.Models.Domain
{
    public class Subscription
    {
        public int SubscriberId { get; set; }
        public Subscriber Subscriber { get; set; } = null!;

        public string LineId { get; set; } = null!;
        public Line Line { get; set; } = null!;
    }
}

