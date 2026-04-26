namespace PulseCRM.Notifications.Domain;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public required string Type { get; set; }
    public required string Message { get; set; }
    public required string DeepLink { get; set; }
    public bool EnrichmentFailed { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ConsumedEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset ConsumedAt { get; set; } = DateTimeOffset.UtcNow;
}
