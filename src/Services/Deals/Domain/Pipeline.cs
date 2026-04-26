namespace PulseCRM.Deals.Domain;

public class Pipeline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Stage> Stages { get; set; } = [];
}
