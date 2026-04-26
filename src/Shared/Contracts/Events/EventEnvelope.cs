namespace PulseCRM.Shared.Contracts.Events;

public record EventEnvelope
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required string EventType { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TenantId { get; init; }
    public required Guid ActorUserId { get; init; }
}
