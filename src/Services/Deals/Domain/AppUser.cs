namespace PulseCRM.Deals.Domain;

public class AppUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string KeycloakSub { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
