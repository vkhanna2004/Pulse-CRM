namespace PulseCRM.Shared.Contracts.Events;
public record DealMoved : EventEnvelope
{
    public required Guid DealId { get; init; }
    public required Guid FromStageId { get; init; }
    public required Guid ToStageId { get; init; }
    public required int PositionInStage { get; init; }
}
