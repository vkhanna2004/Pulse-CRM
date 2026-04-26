using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;

namespace PulseCRM.Deals.Infrastructure;

public class DealsDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DealsDbContext(DbContextOptions<DealsDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ConsumedEvent> ConsumedEvents => Set<ConsumedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tenant query filters
        modelBuilder.Entity<Pipeline>().HasQueryFilter(e => e.TenantId == _tenantContext.Current);
        modelBuilder.Entity<Stage>().HasQueryFilter(e => e.TenantId == _tenantContext.Current);
        modelBuilder.Entity<Deal>().HasQueryFilter(e => e.TenantId == _tenantContext.Current);
        modelBuilder.Entity<Activity>().HasQueryFilter(e => e.TenantId == _tenantContext.Current);
        modelBuilder.Entity<Contact>().HasQueryFilter(e => e.TenantId == _tenantContext.Current);
        modelBuilder.Entity<AppUser>().HasQueryFilter(e => e.TenantId == _tenantContext.Current);

        // Deal rowversion / optimistic concurrency
        modelBuilder.Entity<Deal>()
            .Property(d => d.RowVersion)
            .IsRowVersion();

        // Sparse ordering index
        modelBuilder.Entity<Deal>()
            .HasIndex(d => new { d.StageId, d.PositionInStage });

        // ConsumedEvent dedup index
        modelBuilder.Entity<ConsumedEvent>()
            .HasIndex(e => new { e.TenantId, e.EventId })
            .IsUnique();

        // Activity MentionedUserIds as array column (Npgsql)
        modelBuilder.Entity<Activity>()
            .Property(a => a.MentionedUserIds)
            .HasColumnType("uuid[]");

        // Stage ordering
        modelBuilder.Entity<Stage>()
            .HasIndex(s => new { s.PipelineId, s.Order });

        // Unique index for AppUser KeycloakSub per tenant
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => new { u.TenantId, u.KeycloakSub })
            .IsUnique();

        // Prevent duplicate pipeline per tenant (ensure one default per tenant)
        modelBuilder.Entity<Pipeline>()
            .HasIndex(p => new { p.TenantId })
            .IsUnique();
    }
}
