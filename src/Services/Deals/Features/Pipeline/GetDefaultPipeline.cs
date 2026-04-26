using MediatR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Features.Pipeline;

public record GetDefaultPipelineQuery : IRequest<PipelineDto?>;

public class GetDefaultPipelineHandler : IRequestHandler<GetDefaultPipelineQuery, PipelineDto?>
{
    private readonly DealsDbContext _db;

    public GetDefaultPipelineHandler(DealsDbContext db) => _db = db;

    public async Task<PipelineDto?> Handle(GetDefaultPipelineQuery request, CancellationToken ct)
    {
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .ThenInclude(s => s.Deals.OrderBy(d => d.PositionInStage))
            .ThenInclude(d => d.Owner)
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .ThenInclude(s => s.Deals.OrderBy(d => d.PositionInStage))
            .ThenInclude(d => d.Contact)
            .FirstOrDefaultAsync(ct);

        if (pipeline is null) return null;

        return new PipelineDto(
            pipeline.Id,
            pipeline.Name,
            pipeline.Stages.Select(s => new StageDto(
                s.Id, s.Name, s.Order, s.IsTerminal,
                s.Deals.Select(d => MapDeal(d)).ToList()
            )).ToList()
        );
    }

    private static DealDto MapDeal(Domain.Deal d) => new(
        d.Id, d.TenantId, d.Title, d.Value, d.Currency,
        d.StageId, d.PositionInStage,
        d.OwnerId, d.Owner?.DisplayName ?? "",
        d.ContactId, d.Contact?.Name,
        d.Score, d.ScoreCalculatedAt, d.IsClosed,
        d.ExpectedCloseDate, d.RowVersion,
        d.CreatedAt, d.UpdatedAt
    );
}
