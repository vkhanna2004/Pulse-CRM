using MediatR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Features.Deals;

public record GetDealByIdQuery(Guid DealId) : IRequest<DealDto?>;

public class GetDealByIdHandler : IRequestHandler<GetDealByIdQuery, DealDto?>
{
    private readonly DealsDbContext _db;
    public GetDealByIdHandler(DealsDbContext db) => _db = db;

    public async Task<DealDto?> Handle(GetDealByIdQuery request, CancellationToken ct)
    {
        var deal = await _db.Deals
            .Include(d => d.Owner).Include(d => d.Contact).Include(d => d.Stage)
            .FirstOrDefaultAsync(d => d.Id == request.DealId, ct);
        return deal?.ToDto();
    }
}
