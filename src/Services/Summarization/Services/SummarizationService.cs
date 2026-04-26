using System.Security.Cryptography;
using System.Text;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Shared.Proto.Deals;
using PulseCRM.Summarization.Domain;
using PulseCRM.Summarization.Hubs;
using PulseCRM.Summarization.Infrastructure;

namespace PulseCRM.Summarization.Services;

public class SummarizationService
{
    private readonly SummarizationDbContext _db;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;
    private readonly Client _gemini;
    private readonly IHubContext<AiHub> _hub;
    private readonly ILogger<SummarizationService> _logger;

    // gemini-2.5-flash: latest confirmed Flash model (fast, cost-effective, strong reasoning)
    private const string ModelVersion = "gemini-2.5-flash";

    private static readonly GenerateContentConfig GenerationConfig = new()
    {
        SystemInstruction = new Content
        {
            Parts =
            [
                new Part
                {
                    Text = """
                        You are a CRM assistant. Summarize the deal timeline in 2-3 clear, professional sentences.
                        Focus on: key activities, stage progression, next steps or risks. Be factual and concise.
                        Do not include personal contact details. Do not use markdown.
                        """
                }
            ]
        },
        Temperature = 0.4,    // Low temperature for factual, consistent summaries
        MaxOutputTokens = 256
    };

    public SummarizationService(
        SummarizationDbContext db,
        DealsInternalService.DealsInternalServiceClient dealsClient,
        Client gemini,
        IHubContext<AiHub> hub,
        ILogger<SummarizationService> logger)
    {
        _db = db;
        _dealsClient = dealsClient;
        _gemini = gemini;
        _hub = hub;
        _logger = logger;
    }

    public async Task<bool> ShouldRegenerateAsync(Guid tenantId, Guid dealId, CancellationToken ct = default)
    {
        var existing = await _db.DealSummaries
            .Where(s => s.TenantId == tenantId && s.DealId == dealId)
            .OrderByDescending(s => s.GeneratedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null) return true;
        if (DateTimeOffset.UtcNow - existing.GeneratedAt > TimeSpan.FromHours(24)) return true;

        var currentHash = await ComputeSourceHashAsync(tenantId, dealId, ct);
        return currentHash != existing.SourceHash;
    }

    public async Task GenerateAsync(Guid tenantId, Guid dealId, string triggerReason, CancellationToken ct = default)
    {
        try
        {
            // Fetch deal context and activity timeline from Deals service via gRPC
            var dealContext = await _dealsClient.GetDealContextAsync(new GetDealRequest
            {
                TenantId = tenantId.ToString(),
                DealId = dealId.ToString()
            }, cancellationToken: ct);

            var timeline = await _dealsClient.GetDealTimelineAsync(new GetTimelineRequest
            {
                TenantId = tenantId.ToString(),
                DealId = dealId.ToString(),
                Limit = 20
            }, cancellationToken: ct);

            var sourceHash = await ComputeSourceHashAsync(tenantId, dealId, ct);

            // Build redacted timeline text — PII stripped before sending to LLM
            var timelineText = string.Join("\n", timeline.Activities.Select(a =>
                $"[{a.OccurredAt.ToDateTimeOffset():yyyy-MM-dd}] {a.ActorDisplayName} — {a.Type}: {PiiRedactionService.Redact(a.Content)}"));

            var prompt = $"""
                Deal: {dealContext.Title} (${dealContext.Value:N0})
                Stage: {dealContext.StageName}
                Score: {dealContext.Score}/100

                Timeline (most recent first):
                {timelineText}
                """;

            var summaryBuilder = new StringBuilder();
            long inputTokens = 0, outputTokens = 0, totalTokens = 0;

            // Stream from Gemini — cancellation via WithCancellation on the async enumerable
            var stream = _gemini.Models.GenerateContentStreamAsync(
                model: ModelVersion,
                contents: prompt,
                config: GenerationConfig);

            await foreach (var chunk in stream.WithCancellation(ct))
            {
                var text = chunk.Candidates?[0]?.Content?.Parts?[0]?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    summaryBuilder.Append(text);

                    await _hub.Clients
                        .Group($"tenant:{tenantId}:deal:{dealId}")
                        .SendAsync("SummaryStreaming", new { DealId = dealId, Chunk = text }, ct);
                }

                // UsageMetadata populated on the final chunk; earlier chunks return null
                if (chunk.UsageMetadata is { } usage)
                {
                    inputTokens = usage.PromptTokenCount ?? 0;
                    outputTokens = usage.CandidatesTokenCount ?? 0;
                    totalTokens = usage.TotalTokenCount ?? 0;
                }
            }

            var summary = summaryBuilder.ToString().Trim();

            // Persist summary with token usage for Grafana cost dashboard
            var record = new DealSummary
            {
                DealId = dealId,
                TenantId = tenantId,
                Summary = summary,
                ModelVersion = ModelVersion,
                SourceHash = sourceHash,
                TokenUsage = new Dictionary<string, long>
                {
                    ["input"]  = inputTokens,
                    ["output"] = outputTokens,
                    ["total"]  = totalTokens,
                },
                TriggerReason = triggerReason
            };

            _db.DealSummaries.Add(record);
            await _db.SaveChangesAsync(ct);

            await _hub.Clients
                .Group($"tenant:{tenantId}:deal:{dealId}")
                .SendAsync("SummaryCompleted", new
                {
                    DealId = dealId,
                    Summary = summary,
                    TokenUsage = record.TokenUsage,
                    GeneratedAt = record.GeneratedAt
                }, ct);
        }
        catch (OperationCanceledException)
        {
            // Client navigated away — no error to surface
            _logger.LogDebug("Summary generation cancelled for deal {DealId}", dealId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate summary for deal {DealId}", dealId);
            await _hub.Clients
                .Group($"tenant:{tenantId}:deal:{dealId}")
                .SendAsync("SummaryFailed", new { DealId = dealId, Reason = "Generation failed" }, ct);
        }
    }

    private async Task<string> ComputeSourceHashAsync(Guid tenantId, Guid dealId, CancellationToken ct)
    {
        var timeline = await _dealsClient.GetDealTimelineAsync(new GetTimelineRequest
        {
            TenantId = tenantId.ToString(),
            DealId = dealId.ToString(),
            Limit = 100
        }, cancellationToken: ct);

        var raw = string.Join(",", timeline.Activities.Select(a => $"{a.Id}:{a.OccurredAt}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
