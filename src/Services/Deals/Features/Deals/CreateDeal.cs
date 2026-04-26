using MediatR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Features.Deals;

public record CreateDealCommand(
    string Title,
    decimal Value,
    string Currency,
    Guid StageId,
    Guid OwnerId,
    Guid? ContactId,
    DateTimeOffset? ExpectedCloseDate
) : IRequest<DealDto>;

public class CreateDealHandler : IRequestHandler<CreateDealCommand, DealDto>
{
    private readonly DealsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly EventPublisher _events;

    public CreateDealHandler(DealsDbContext db, ITenantContext tenant, EventPublisher events)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
    }

    public async Task<DealDto> Handle(CreateDealCommand cmd, CancellationToken ct)
    {
        // Validate StageId belongs to current tenant
        var stage = await _db.Stages.FirstOrDefaultAsync(s => s.Id == cmd.StageId, ct)
            ?? throw new KeyNotFoundException($"Stage {cmd.StageId} not found in current tenant");

        // Validate OwnerId belongs to current tenant
        var ownerExists = await _db.Users.AnyAsync(u => u.Id == cmd.OwnerId, ct);
        if (!ownerExists) throw new KeyNotFoundException($"User {cmd.OwnerId} not found in current tenant");

        var maxPos = await _db.Deals
            .Where(d => d.StageId == cmd.StageId)
            .Select(d => (int?)d.PositionInStage)
            .MaxAsync(ct) ?? 0;

        var deal = new Deal
        {
            TenantId = _tenant.Current,
            Title = cmd.Title,
            Value = cmd.Value,
            Currency = cmd.Currency,
            StageId = cmd.StageId,
            OwnerId = cmd.OwnerId,
            ContactId = cmd.ContactId,
            ExpectedCloseDate = cmd.ExpectedCloseDate,
            PositionInStage = maxPos + 1000,
            StageChangedAt = DateTimeOffset.UtcNow
        };

        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);

        await _events.PublishDealCreated(deal.Id, deal.StageId, deal.OwnerId, deal.Value, deal.ContactId, _tenant.CurrentUserId);

        return deal.ToDto();
    }
}
