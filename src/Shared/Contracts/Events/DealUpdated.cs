namespace PulseCRM.Shared.Contracts.Events;
public record DealUpdated : EventEnvelope
{
    public required Guid DealId { get; init; }
    public required IReadOnlyList<string> ChangedFields { get; init; }
}
