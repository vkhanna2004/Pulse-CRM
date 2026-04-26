using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Hubs;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Events;

namespace PulseCRM.Deals.Consumers;

public class ScoringCompletedConsumer : IConsumer<ScoringCompleted>
{
    private readonly DealsDbContext _db;
    private readonly IHubContext<PipelineHub> _hub;

    public ScoringCompletedConsumer(DealsDbContext db, IHubContext<PipelineHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task Consume(ConsumeContext<ScoringCompleted> context)
    {
        var msg = context.Message;

        var updated = await _db.Deals
            .IgnoreQueryFilters()
            .Where(d => d.Id == msg.DealId && d.TenantId == msg.TenantId)
            .Where(d => d.ScoreCalculatedAt == null || d.ScoreCalculatedAt < msg.CalculatedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Score, msg.Score)
                .SetProperty(d => d.ScoreCalculatedAt, msg.CalculatedAt));

        if (updated > 0)
        {
            // Look up the pipeline ID for the correct group name
            var deal = await _db.Deals
                .IgnoreQueryFilters()
                .Include(d => d.Stage)
                .FirstOrDefaultAsync(d => d.Id == msg.DealId && d.TenantId == msg.TenantId);

            if (deal?.Stage is not null)
            {
                await _hub.Clients
                    .Group($"tenant:{msg.TenantId}:pipeline:{deal.Stage.PipelineId}")
                    .SendAsync("DealScoreUpdated", new
                    {
                        DealId = msg.DealId,
                        Score = msg.Score,
                        Factors = msg.Factors
                    });
            }
        }
    }
}
