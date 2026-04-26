namespace PulseCRM.Shared.Contracts.Events;
public record DealAssigned : EventEnvelope
{
    public required Guid DealId { get; init; }
    public Guid? PreviousOwnerId { get; init; }
    public required Guid NewOwnerId { get; init; }
}
