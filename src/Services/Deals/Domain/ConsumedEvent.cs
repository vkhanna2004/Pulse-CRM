namespace PulseCRM.Deals.Domain;

public class ConsumedEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset ConsumedAt { get; set; } = DateTimeOffset.UtcNow;
}
