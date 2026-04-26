using Microsoft.EntityFrameworkCore;
using PulseCRM.Notifications.Domain;

namespace PulseCRM.Notifications.Infrastructure;

public interface INotificationsTenantContext
{
    Guid Current { get; }
}

public class NotificationsTenantContext : INotificationsTenantContext
{
    public Guid Current { get; private set; }
    public void Set(Guid tenantId) => Current = tenantId;
}

public class NotificationsDbContext : DbContext
{
    private readonly INotificationsTenantContext _tenantContext;

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, INotificationsTenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ConsumedEvent> ConsumedEvents => Set<ConsumedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>()
            .HasQueryFilter(n => n.TenantId == _tenantContext.Current);
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.TenantId, n.UserId, n.CreatedAt });
        modelBuilder.Entity<ConsumedEvent>()
            .HasIndex(e => new { e.TenantId, e.EventId }).IsUnique();
    }
}
