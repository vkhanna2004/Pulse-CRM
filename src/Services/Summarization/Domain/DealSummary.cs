namespace PulseCRM.Summarization.Domain;

public class DealSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DealId { get; set; }
    public Guid TenantId { get; set; }
    public required string Summary { get; set; }
    public required string ModelVersion { get; set; }
    public required string SourceHash { get; set; }
    public required Dictionary<string, long> TokenUsage { get; set; }
    public required string TriggerReason { get; set; } // Manual | ActivityThreshold | StaleRefresh
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ConsumedEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset ConsumedAt { get; set; } = DateTimeOffset.UtcNow;
}
