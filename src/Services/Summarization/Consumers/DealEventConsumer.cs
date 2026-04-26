using MassTransit;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Shared.Contracts.Events;
using PulseCRM.Summarization.Infrastructure;
using PulseCRM.Shared.Proto.Deals;

namespace PulseCRM.Summarization.Consumers;

public class DealEventConsumer :
    IConsumer<DealCreated>,
    IConsumer<DealMoved>,
    IConsumer<DealActivityAdded>
{
    private readonly SummarizationDbContext _db;
    private readonly Services.SummarizationService _summarization;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;

    public DealEventConsumer(SummarizationDbContext db, Services.SummarizationService summarization, DealsInternalService.DealsInternalServiceClient dealsClient)
    {
        _db = db;
        _summarization = summarization;
        _dealsClient = dealsClient;
    }

    public Task Consume(ConsumeContext<DealCreated> context) => HandleAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);
    public Task Consume(ConsumeContext<DealMoved> context)   => HandleAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);
    public Task Consume(ConsumeContext<DealActivityAdded> context) => HandleAsync(context.Message.EventId, context.Message.TenantId, context.Message.DealId);

    private async Task HandleAsync(Guid eventId, Guid tenantId, Guid dealId)
    {
        _db.ConsumedEvents.Add(new Domain.ConsumedEvent { TenantId = tenantId, EventId = eventId });
        try { await _db.SaveChangesAsync(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return; }

        // Get actual activity count from Deals service
        int activityCount;
        try
        {
            var timeline = await _dealsClient.GetDealTimelineAsync(new GetTimelineRequest
            {
                TenantId = tenantId.ToString(),
                DealId = dealId.ToString(),
                Limit = 1 // We just need the count signal; use ShouldRegenerateAsync for hash check
            });
            activityCount = timeline.Activities.Count;
        }
        catch
        {
            // Fall back to hash-based check only
            activityCount = 1; // Non-zero to trigger ShouldRegenerateAsync check
        }

        var shouldRegenerate = await _summarization.ShouldRegenerateAsync(tenantId, dealId);
        if (shouldRegenerate)
            await _summarization.GenerateAsync(tenantId, dealId, "ActivityThreshold");
    }
}
