namespace PulseCRM.Shared.Contracts.Events;
public record DealActivityAdded : EventEnvelope
{
    public required Guid DealId { get; init; }
    public required string ActivityType { get; init; }
    public required Guid ActivityId { get; init; }
}
