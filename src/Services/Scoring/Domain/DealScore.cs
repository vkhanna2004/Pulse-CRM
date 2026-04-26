namespace PulseCRM.Scoring.Domain;

public class DealScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DealId { get; set; }
    public Guid TenantId { get; set; }
    public int Score { get; set; }
    public Dictionary<string, double> Factors { get; set; } = [];
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ConsumedEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset ConsumedAt { get; set; } = DateTimeOffset.UtcNow;
}
