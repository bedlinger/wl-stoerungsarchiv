using wls_backend.Models.Enums;

namespace wls_backend.Models.Domain
{
    public record DisturbanceEvent(EventType Type, Disturbance Disturbance, string? UpdateText = null);
}

