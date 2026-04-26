namespace PulseCRM.Shared.Contracts.Events;
public record ScoringCompleted : EventEnvelope
{
    public required Guid DealId { get; init; }
    public required int Score { get; init; }
    public required DateTimeOffset CalculatedAt { get; init; }
    public required Dictionary<string, double> Factors { get; init; }
}
