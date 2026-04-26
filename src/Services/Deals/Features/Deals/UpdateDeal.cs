using MediatR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Features.Deals;

public record UpdateDealCommand(
    Guid DealId,
    string? Title,
    decimal? Value,
    Guid? OwnerId,
    Guid? ContactId,
    DateTimeOffset? ExpectedCloseDate,
    Guid ActorUserId
) : IRequest<DealDto>;

public class UpdateDealHandler : IRequestHandler<UpdateDealCommand, DealDto>
{
    private readonly DealsDbContext _db;
    private readonly EventPublisher _events;

    public UpdateDealHandler(DealsDbContext db, EventPublisher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<DealDto> Handle(UpdateDealCommand cmd, CancellationToken ct)
    {
        var deal = await _db.Deals
            .Include(d => d.Owner)
            .Include(d => d.Contact)
            .Include(d => d.Stage)
            .FirstOrDefaultAsync(d => d.Id == cmd.DealId, ct)
            ?? throw new KeyNotFoundException($"Deal {cmd.DealId} not found");

        var changedFields = new List<string>();

        if (cmd.Title is not null && cmd.Title != deal.Title) { deal.Title = cmd.Title; changedFields.Add("title"); }
        if (cmd.Value.HasValue && cmd.Value != deal.Value) { deal.Value = cmd.Value.Value; changedFields.Add("value"); }
        if (cmd.OwnerId.HasValue && cmd.OwnerId != deal.OwnerId)
        {
            var prevOwner = deal.OwnerId;
            deal.OwnerId = cmd.OwnerId.Value;
            changedFields.Add("ownerId");
            // Write Assignment activity
            _db.Activities.Add(new Domain.Activity
            {
                TenantId = deal.TenantId,
                DealId = deal.Id,
                ActorUserId = cmd.ActorUserId,
                Type = Domain.ActivityTypes.Assignment,
                Content = $"Reassigned deal"
            });
        }
        if (cmd.ContactId != deal.ContactId) { deal.ContactId = cmd.ContactId; changedFields.Add("contactId"); }
        if (cmd.ExpectedCloseDate != deal.ExpectedCloseDate) { deal.ExpectedCloseDate = cmd.ExpectedCloseDate; changedFields.Add("expectedCloseDate"); }

        deal.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (changedFields.Count > 0)
            await _events.PublishDealUpdated(deal.Id, changedFields, cmd.ActorUserId);
        if (changedFields.Contains("ownerId"))
            await _events.PublishDealAssigned(deal.Id, null, cmd.OwnerId!.Value, cmd.ActorUserId);

        return deal.ToDto();
    }
}
