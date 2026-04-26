using MediatR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Features.Deals;

public record LogActivityCommand(Guid DealId, string Type, string? Content, Guid ActorUserId) : IRequest<ActivityDto>;

public class LogActivityHandler : IRequestHandler<LogActivityCommand, ActivityDto>
{
    private readonly DealsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly EventPublisher _events;

    public LogActivityHandler(DealsDbContext db, ITenantContext tenant, EventPublisher events)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
    }

    public async Task<ActivityDto> Handle(LogActivityCommand cmd, CancellationToken ct)
    {
        if (!new[] { ActivityTypes.Call, ActivityTypes.Email }.Contains(cmd.Type))
            throw new ArgumentException($"Invalid activity type: {cmd.Type}");

        var dealExists = await _db.Deals.AnyAsync(d => d.Id == cmd.DealId, ct);
        if (!dealExists) throw new KeyNotFoundException($"Deal {cmd.DealId} not found");

        var activity = new Activity
        {
            TenantId = _tenant.Current,
            DealId = cmd.DealId,
            ActorUserId = cmd.ActorUserId,
            Type = cmd.Type,
            Content = cmd.Content
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);

        await _events.PublishDealActivityAdded(cmd.DealId, cmd.Type, activity.Id, cmd.ActorUserId);

        var actor = await _db.Users.FindAsync([cmd.ActorUserId], ct);
        return activity.ToDto(actor?.DisplayName ?? "Unknown");
    }
}
