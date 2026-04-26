using Microsoft.EntityFrameworkCore;
using PulseCRM.Scoring.Domain;

namespace PulseCRM.Scoring.Infrastructure;

public class ScoringDbContext : DbContext
{
    public ScoringDbContext(DbContextOptions<ScoringDbContext> options) : base(options) { }

    public DbSet<DealScore> DealScores => Set<DealScore>();
    public DbSet<ConsumedEvent> ConsumedEvents => Set<ConsumedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DealScore>()
            .HasIndex(s => new { s.TenantId, s.DealId })
            .IsUnique();

        modelBuilder.Entity<DealScore>()
            .Property(s => s.Factors)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, double>());

        modelBuilder.Entity<ConsumedEvent>()
            .HasIndex(e => new { e.TenantId, e.EventId })
            .IsUnique();
    }
}
