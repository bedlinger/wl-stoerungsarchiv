namespace wls_backend.Models.Domain
{
    public class Subscription
    {
        public int DeviceId { get; set; }
        public Device Device { get; set; } = null!;

        public string LineId { get; set; } = null!;
        public Line Line { get; set; } = null!;
    }
}

