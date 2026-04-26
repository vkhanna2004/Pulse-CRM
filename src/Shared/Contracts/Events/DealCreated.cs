namespace PulseCRM.Shared.Contracts.Events;
public record DealCreated : EventEnvelope
{
    public required Guid DealId { get; init; }
    public required Guid StageId { get; init; }
    public required Guid OwnerId { get; init; }
    public required decimal Value { get; init; }
    public Guid? ContactId { get; init; }
}
