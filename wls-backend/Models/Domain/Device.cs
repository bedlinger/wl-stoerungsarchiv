namespace wls_backend.Models.Domain
{
    public class Device
    {
        public int Id { get; set; }
        required public string Token { get; set; }
        public ICollection<Subscription> Subscriptions { get; set; } = [];
    }
}
