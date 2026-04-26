namespace PulseCRM.Deals.Domain;

public class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DealId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string Type { get; set; } // Note | Call | Email | StageChange | Assignment
    public string? Content { get; set; }
    public Guid[]? MentionedUserIds { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Deal Deal { get; set; } = null!;
    
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(ActorUserId))]
    public AppUser Actor { get; set; } = null!;
}

public static class ActivityTypes
{
    public const string Note = "Note";
    public const string Call = "Call";
    public const string Email = "Email";
    public const string StageChange = "StageChange";
    public const string Assignment = "Assignment";
}
