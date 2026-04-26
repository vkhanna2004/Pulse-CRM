using Microsoft.EntityFrameworkCore;

namespace PulseCRM.Scoring.Infrastructure;

public class ConsumedEventCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConsumedEventCleanupJob> _logger;

    public ConsumedEventCleanupJob(IServiceScopeFactory scopeFactory, ILogger<ConsumedEventCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ScoringDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
                await db.ConsumedEvents.Where(e => e.ConsumedAt < cutoff).ExecuteDeleteAsync(stoppingToken);
                _logger.LogInformation("Purged consumed events older than 30 days");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error during consumed event cleanup");
            }
        }
    }
}
