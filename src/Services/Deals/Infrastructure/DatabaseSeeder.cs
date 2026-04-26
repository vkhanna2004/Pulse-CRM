using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;

namespace PulseCRM.Deals.Infrastructure;

public class DatabaseSeeder
{
    public static async Task SeedAsync(DealsDbContext db)
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Bypass tenant filter for seeding
        if (await db.Pipelines.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId))
            return;

        var pipeline = new Pipeline { TenantId = tenantId, Name = "Sales Pipeline" };
        var stages = new[]
        {
            new Stage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Lead",         Order = 1 },
            new Stage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Qualified",    Order = 2 },
            new Stage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Proposal",     Order = 3 },
            new Stage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Negotiation",  Order = 4 },
            new Stage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Won",          Order = 5, IsTerminal = true },
            new Stage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Lost",         Order = 6, IsTerminal = true },
        };
        pipeline.Stages.AddRange(stages);

        // IDs must match the fixed UUIDs set in infrastructure/keycloak/realm-export.json
        var aliceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var bobId   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        var alice = new AppUser
        {
            Id = aliceId,
            TenantId = tenantId,
            KeycloakSub = "aaaaaaaa-0000-0000-0000-000000000001",
            DisplayName = "Alice Demo",
            Email = "alice@pulsecrm.dev"
        };
        var bob = new AppUser
        {
            Id = bobId,
            TenantId = tenantId,
            KeycloakSub = "bbbbbbbb-0000-0000-0000-000000000002",
            DisplayName = "Bob Demo",
            Email = "bob@pulsecrm.dev"
        };

        var now = DateTimeOffset.UtcNow;
        var deals = new[]
        {
            new Deal { TenantId = tenantId, Title = "Acme Corp — Q2 Expansion",    Value = 45000,  StageId = stages[2].Id, OwnerId = aliceId, PositionInStage = 1000, StageChangedAt = now },
            new Deal { TenantId = tenantId, Title = "Globex — Enterprise Licence", Value = 120000, StageId = stages[1].Id, OwnerId = bobId,   PositionInStage = 1000, StageChangedAt = now },
            new Deal { TenantId = tenantId, Title = "Initech — Support Contract",  Value = 18000,  StageId = stages[0].Id, OwnerId = aliceId, PositionInStage = 1000, StageChangedAt = now },
        };

        db.Pipelines.Add(pipeline);
        db.Users.AddRange(alice, bob);
        db.Deals.AddRange(deals);
        await db.SaveChangesAsync();
    }
}
