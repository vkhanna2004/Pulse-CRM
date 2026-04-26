using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Hubs;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Features.Deals;

public record MoveDealCommand(
    Guid DealId,
    Guid ToStageId,
    int PositionInStage,
    uint RowVersion,
    Guid ActorUserId
) : IRequest<DealDto>;

public class MoveDealHandler : IRequestHandler<MoveDealCommand, DealDto>
{
    private readonly DealsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly EventPublisher _events;
    private readonly IHubContext<PipelineHub> _hub;

    public MoveDealHandler(DealsDbContext db, ITenantContext tenant, EventPublisher events, IHubContext<PipelineHub> hub)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _hub = hub;
    }

    public async Task<DealDto> Handle(MoveDealCommand cmd, CancellationToken ct)
    {
        var deal = await _db.Deals
            .Include(d => d.Stage).ThenInclude(s => s.Pipeline)
            .Include(d => d.Owner)
            .Include(d => d.Contact)
            .FirstOrDefaultAsync(d => d.Id == cmd.DealId, ct)
            ?? throw new KeyNotFoundException($"Deal {cmd.DealId} not found");

        // Validate target stage belongs to same pipeline and tenant
        var toStage = await _db.Stages
            .FirstOrDefaultAsync(s => s.Id == cmd.ToStageId, ct)
            ?? throw new KeyNotFoundException($"Stage {cmd.ToStageId} not found in current tenant");

        if (toStage.PipelineId != deal.Stage.PipelineId)
            throw new InvalidOperationException("Cannot move deal to a stage in a different pipeline");

        deal.RowVersion = cmd.RowVersion;

        var fromStageId = deal.StageId;
        deal.StageId = cmd.ToStageId;
        deal.PositionInStage = cmd.PositionInStage;
        deal.UpdatedAt = DateTimeOffset.UtcNow;
        deal.StageChangedAt = DateTimeOffset.UtcNow;

        _db.Activities.Add(new Activity
        {
            TenantId = _tenant.Current,
            DealId = deal.Id,
            ActorUserId = cmd.ActorUserId,
            Type = ActivityTypes.StageChange,
            Content = $"Moved from {deal.Stage.Name} to {toStage.Name}"
        });

        await _db.SaveChangesAsync(ct);

        var dto = deal.ToDto();

        // Broadcast to SignalR pipeline group
        await _hub.Clients
            .Group($"tenant:{_tenant.Current}:pipeline:{deal.Stage.PipelineId}")
            .SendAsync("DealMoved", new
            {
                DealId = deal.Id,
                FromStageId = fromStageId,
                ToStageId = cmd.ToStageId,
                PositionInStage = cmd.PositionInStage,
                RowVersion = deal.RowVersion
            }, ct);

        await _events.PublishDealMoved(deal.Id, fromStageId, cmd.ToStageId, cmd.PositionInStage, cmd.ActorUserId);

        return dto;
    }
}
