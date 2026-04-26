using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Scoring.Algorithm;
using PulseCRM.Scoring.Infrastructure;
using PulseCRM.Shared.Proto.Scoring;
using PulseCRM.Shared.Proto.Deals;

using MassTransit;
using PulseCRM.Shared.Contracts.Events;

namespace PulseCRM.Scoring.Grpc;

[AllowAnonymous]
public class ScoringGrpcService : ScoringService.ScoringServiceBase
{
    private readonly ScoringDbContext _db;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;
    private readonly IBus _bus;
    private readonly ILogger<ScoringGrpcService> _logger;

    public ScoringGrpcService(ScoringDbContext db, DealsInternalService.DealsInternalServiceClient dealsClient, IBus bus, ILogger<ScoringGrpcService> logger)
    {
        _db = db;
        _dealsClient = dealsClient;
        _bus = bus;
        _logger = logger;
    }

    public override async Task<ScoreResponse> GetScore(GetScoreRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DealId, out var dealId) || !Guid.TryParse(request.TenantId, out var tenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid deal_id or tenant_id format"));

        var score = await _db.DealScores.FirstOrDefaultAsync(s => s.DealId == dealId && s.TenantId == tenantId);
        return score is null
            ? await ComputeAndStore(request.TenantId, request.DealId)
            : ToProto(score);
    }

    public override async Task<BatchScoreResponse> GetScoresBatch(BatchScoreRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TenantId, out var tenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant_id format"));

        var dealIds = request.DealIds.Select(id =>
            Guid.TryParse(id, out var g) ? (Guid?)g : null).Where(g => g.HasValue).Select(g => g!.Value).ToList();

        var existing = await _db.DealScores
            .Where(s => s.TenantId == tenantId && dealIds.Contains(s.DealId))
            .ToListAsync();

        var existingIds = existing.Select(s => s.DealId).ToHashSet();
        var missing = dealIds.Except(existingIds).ToList();

        // Compute missing scores lazily
        var computed = new List<Domain.DealScore>();
        foreach (var id in missing)
        {
            try
            {
                var score = await ComputeAndStore(tenantId.ToString(), id.ToString());
                var stored = await _db.DealScores.FirstOrDefaultAsync(s => s.DealId == id && s.TenantId == tenantId);
                if (stored is not null) computed.Add(stored);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to compute score for deal {DealId}", id); }
        }

        var response = new BatchScoreResponse();
        response.Scores.AddRange(existing.Concat(computed).Select(ToProto));
        return response;
    }

    public override async Task<ScoreResponse> RecalculateScore(RecalculateRequest request, ServerCallContext context)
    {
        return await ComputeAndStore(request.TenantId, request.DealId);
    }

    private async Task<ScoreResponse> ComputeAndStore(string tenantId, string dealId)
    {
        if (!Guid.TryParse(dealId, out var dealGuid) || !Guid.TryParse(tenantId, out var tenantGuid))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID format"));

        var snapshot = await _dealsClient.GetDealSnapshotAsync(new GetDealRequest
        {
            TenantId = tenantId,
            DealId = dealId
        });

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

        var existing = await _db.DealScores.FirstOrDefaultAsync(s => s.DealId == dealGuid && s.TenantId == tenantGuid);
        if (existing is null)
        {
            existing = new Domain.DealScore { DealId = dealGuid, TenantId = tenantGuid };
            _db.DealScores.Add(existing);
        }

        existing.Score = result.Score;
        existing.Factors = result.Factors;
        existing.CalculatedAt = now;

        await _db.SaveChangesAsync();

        await _bus.Publish(new ScoringCompleted
        {
            EventType = "ScoringCompleted",
            TenantId = tenantGuid,
            ActorUserId = Guid.Empty, // Represents system recalculation
            DealId = dealGuid,
            Score = result.Score,
            CalculatedAt = now,
            Factors = result.Factors
        });

        return ToProto(existing);
    }

    private static ScoreResponse ToProto(Domain.DealScore s)
    {
        var response = new ScoreResponse
        {
            DealId = s.DealId.ToString(),
            Score = s.Score,
            CalculatedAt = Timestamp.FromDateTimeOffset(s.CalculatedAt)
        };
        foreach (var (k, v) in s.Factors) response.Factors[k] = v;
        return response;
    }
}
