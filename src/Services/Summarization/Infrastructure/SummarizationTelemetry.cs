using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseCRM.Shared.Contracts.Telemetry;

namespace PulseCRM.Summarization.Infrastructure;

public static class SummarizationTelemetry
{
    public static readonly Meter Meter = new("PulseCRM.Summarization");

    public static readonly Counter<long> TokensInputTotal =
        Meter.CreateCounter<long>("pulsecrm.ai.tokens.input_total");

    public static readonly Counter<long> TokensOutputTotal =
        Meter.CreateCounter<long>("pulsecrm.ai.tokens.output_total");

    public static readonly Counter<long> TokensCacheReadTotal =
        Meter.CreateCounter<long>("pulsecrm.ai.tokens.cache_read_total");

    public static readonly Histogram<double> GenerationDuration =
        Meter.CreateHistogram<double>("pulsecrm.ai.summary.generation_duration_seconds", unit: "s");

    public static readonly Counter<long> SummaryTriggers =
        Meter.CreateCounter<long>("pulsecrm.ai.summary.trigger_count_total");

    public static IServiceCollection AddSummarizationTelemetry(this IServiceCollection services, IConfiguration configuration)
        => services.AddPulseCrmTelemetry(configuration, "pulsecrm-summarization",
            configureMetrics: m => m.AddMeter("PulseCRM.Summarization"));
}
