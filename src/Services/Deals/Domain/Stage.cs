namespace PulseCRM.Deals.Domain;

public class Stage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PipelineId { get; set; }
    public required string Name { get; set; }
    public int Order { get; set; }
    public bool IsTerminal { get; set; }
    public Pipeline Pipeline { get; set; } = null!;
    public List<Deal> Deals { get; set; } = [];
}
