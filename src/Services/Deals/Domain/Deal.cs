namespace PulseCRM.Deals.Domain;

public class Deal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Title { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid StageId { get; set; }
    public int PositionInStage { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ContactId { get; set; }
    public int Score { get; set; }
    public DateTimeOffset? ScoreCalculatedAt { get; set; }
    public DateTimeOffset? ExpectedCloseDate { get; set; }
    public DateTimeOffset? StageChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public uint RowVersion { get; set; }

    public Stage Stage { get; set; } = null!;
    public AppUser Owner { get; set; } = null!;
    public Contact? Contact { get; set; }
    public List<Activity> Activities { get; set; } = [];

    public bool IsClosed => Stage?.IsTerminal ?? false;
}
