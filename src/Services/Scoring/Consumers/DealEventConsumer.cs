using MassTransit;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Scoring.Algorithm;
using PulseCRM.Scoring.Domain;
using PulseCRM.Scoring.Infrastructure;
using PulseCRM.Shared.Contracts.Events;
using PulseCRM.Shared.Proto.Deals;

namespace PulseCRM.Scoring.Consumers;

public class DealEventConsumer :
    IConsumer<DealCreated>,
    IConsumer<DealMoved>,
    IConsumer<DealUpdated>,
    IConsumer<DealActivityAdded>
{
    private readonly ScoringDbContext _db;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;
    private readonly IPublishEndpoint _bus;

    public DealEventConsumer(
        ScoringDbContext db,
        DealsInternalService.DealsInternalServiceClient dealsClient,
        IPublishEndpoint bus)
    {
        _db = db;
        _dealsClient = dealsClient;
        _bus = bus;
    }

    public Task Consume(ConsumeContext<DealCreated> context)      => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);
    public Task Consume(ConsumeContext<DealMoved> context)        => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);
    public Task Consume(ConsumeContext<DealUpdated> context)      => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);
    public Task Consume(ConsumeContext<DealActivityAdded> context) => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);

    private async Task ProcessAsync(Guid eventId, Guid tenantId, Guid dealId)
    {
        // Check dedup first
        var alreadySeen = await _db.ConsumedEvents.AnyAsync(e => e.TenantId == tenantId && e.EventId == eventId);
        if (alreadySeen) return;

        DealSnapshot snapshot;
        try
        {
            snapshot = await _dealsClient.GetDealSnapshotAsync(new GetDealRequest
            {
                TenantId = tenantId.ToString(),
                DealId = dealId.ToString()
            });
        }
        catch (Exception ex)
        {
            // If Deals is down, let MassTransit retry
            throw new Exception($"Failed to fetch snapshot for deal {dealId}", ex);
        }

        var input = new ScoringInput(
            Value: snapshot.Value,
            StageOrder: snapshot.StageOrder,
            MaxStageOrder: snapshot.MaxStageOrder > 0 ? snapshot.MaxStageOrder : 6,
            DaysInStage: snapshot.DaysInStage,
            ActivityCount30d: snapshot.ActivityCount30D,
            MaxTenantValue: snapshot.MaxTenantValue > 0 ? snapshot.MaxTenantValue : 500_000
        );

        var result = LeadScoringAlgorithm.Calculate(input);
        var now = DateTimeOffset.UtcNow;

        var existing = await _db.DealScores.FirstOrDefaultAsync(s => s.DealId == dealId && s.TenantId == tenantId);
        if (existing is null)
        {
            existing = new DealScore { DealId = dealId, TenantId = tenantId };
            _db.DealScores.Add(existing);
        }

        existing.Score = result.Score;
        existing.Factors = result.Factors;
        existing.CalculatedAt = now;

        // Insert dedup row atomically with score update
        _db.ConsumedEvents.Add(new ConsumedEvent { TenantId = tenantId, EventId = eventId });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Dedup unique constraint violated — another instance processed this event concurrently
            return;
        }

        // Publish AFTER successful save — if this fails, MassTransit retries but dedup prevents double-processing
        await _bus.Publish(new ScoringCompleted
        {
            EventType = "ScoringCompleted",
            TenantId = tenantId,
            ActorUserId = Guid.Empty,
            DealId = dealId,
            Score = result.Score,
            CalculatedAt = now,
            Factors = result.Factors
        });
    }
}
