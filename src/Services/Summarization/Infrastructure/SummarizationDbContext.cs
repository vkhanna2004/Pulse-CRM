using Microsoft.EntityFrameworkCore;
using PulseCRM.Summarization.Domain;

namespace PulseCRM.Summarization.Infrastructure;

public class SummarizationDbContext : DbContext
{
    public SummarizationDbContext(DbContextOptions<SummarizationDbContext> options) : base(options) { }

    public DbSet<DealSummary> DealSummaries => Set<DealSummary>();
    public DbSet<ConsumedEvent> ConsumedEvents => Set<ConsumedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DealSummary>()
            .HasIndex(s => new { s.TenantId, s.DealId });
        modelBuilder.Entity<DealSummary>()
            .Property(s => s.TokenUsage)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, long>());
        modelBuilder.Entity<ConsumedEvent>()
            .HasIndex(e => new { e.TenantId, e.EventId }).IsUnique();
    }
}
