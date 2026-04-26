using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Proto.Deals;
using Google.Protobuf.WellKnownTypes;

namespace PulseCRM.Deals.Grpc;

[AllowAnonymous]
public class DealsGrpcService : DealsInternalService.DealsInternalServiceBase
{
    private readonly DealsDbContext _db;

    public DealsGrpcService(DealsDbContext db) => _db = db;

    public override async Task<DealContextResponse> GetDealContext(GetDealRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DealId, out var dealId) || !Guid.TryParse(request.TenantId, out var tenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid deal_id or tenant_id format"));

        var deal = await _db.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Owner)
            .Include(d => d.Contact)
            .Include(d => d.Stage)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.TenantId == tenantId)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"Deal {request.DealId} not found"));

        return new DealContextResponse
        {
            DealId = deal.Id.ToString(),
            Title = deal.Title,
            Value = (double)deal.Value,
            OwnerDisplayName = deal.Owner?.DisplayName ?? "",
            OwnerId = deal.OwnerId.ToString(),
            StageName = deal.Stage?.Name ?? "",
            Score = deal.Score,
            ContactDisplayName = deal.Contact?.Name ?? ""
        };
    }

    public override async Task<TimelineResponse> GetDealTimeline(GetTimelineRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DealId, out var dealId) || !Guid.TryParse(request.TenantId, out var tenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid deal_id or tenant_id format"));

        var limit = request.Limit > 0 ? Math.Min(request.Limit, 200) : 50;

        var query = _db.Activities
            .IgnoreQueryFilters()
            .Include(a => a.Actor)
            .Where(a => a.DealId == dealId && a.TenantId == tenantId);

        if (request.Since is not null)
            query = query.Where(a => a.CreatedAt > request.Since.ToDateTimeOffset());

        var activities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var response = new TimelineResponse();
        response.Activities.AddRange(activities.Select(a => new ActivityItem
        {
            Id = a.Id.ToString(),
            Type = a.Type,
            ActorDisplayName = a.Actor?.DisplayName ?? "",
            Content = a.Content ?? "",
            OccurredAt = Timestamp.FromDateTimeOffset(a.CreatedAt)
        }));
        return response;
    }

    public override async Task<DealSnapshot> GetDealSnapshot(GetDealRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DealId, out var dealId) || !Guid.TryParse(request.TenantId, out var tenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid deal_id or tenant_id format"));

        var deal = await _db.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Stage).ThenInclude(s => s.Pipeline)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.TenantId == tenantId)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"Deal {request.DealId} not found"));

        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var activityCount = await _db.Activities
            .IgnoreQueryFilters()
            .CountAsync(a => a.DealId == dealId && a.TenantId == tenantId && a.CreatedAt >= thirtyDaysAgo);

        // Use StageChangedAt for accurate days-in-stage
        var stageChangedAt = deal.StageChangedAt ?? deal.CreatedAt;
        var daysInStage = (int)(DateTimeOffset.UtcNow - stageChangedAt).TotalDays;

        // Max stage order from pipeline for normalization
        var maxStageOrder = await _db.Stages
            .IgnoreQueryFilters()
            .Where(s => s.PipelineId == deal.Stage.PipelineId)
            .MaxAsync(s => s.Order);

        return new DealSnapshot
        {
            Value = (double)deal.Value,
            StageOrder = deal.Stage?.Order ?? 0,
            MaxStageOrder = maxStageOrder,
            DaysInStage = daysInStage,
            ActivityCount30D = activityCount,
            ExpectedCloseDate = deal.ExpectedCloseDate.HasValue
                ? Timestamp.FromDateTimeOffset(deal.ExpectedCloseDate.Value)
                : null
        };
    }
}
