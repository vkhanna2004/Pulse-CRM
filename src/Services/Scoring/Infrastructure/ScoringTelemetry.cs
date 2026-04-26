using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseCRM.Shared.Contracts.Telemetry;

namespace PulseCRM.Scoring.Infrastructure;

public static class ScoringTelemetry
{
    public static readonly Meter Meter = new("PulseCRM.Scoring");

    public static readonly Histogram<double> CalculationDuration =
        Meter.CreateHistogram<double>("pulsecrm.scoring.calculation.duration_seconds", unit: "s", description: "Duration of score calculations");

    public static readonly Counter<long> EventsConsumed =
        Meter.CreateCounter<long>("pulsecrm.events.consumed.total", description: "Events consumed from RabbitMQ");

    public static IServiceCollection AddScoringTelemetry(this IServiceCollection services, IConfiguration configuration)
        => services.AddPulseCrmTelemetry(configuration, "pulsecrm-scoring",
            configureMetrics: m => m.AddMeter("PulseCRM.Scoring"));
}
