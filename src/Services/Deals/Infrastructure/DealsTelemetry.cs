using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PulseCRM.Shared.Contracts.Telemetry;

namespace PulseCRM.Deals.Infrastructure;

public static class DealsTelemetry
{
    public static readonly Meter Meter = new("PulseCRM.Deals");

    public static readonly Counter<long> DealMoves =
        Meter.CreateCounter<long>("pulsecrm.deals.moves.total", description: "Total kanban deal moves");

    public static readonly Counter<long> EventsPublished =
        Meter.CreateCounter<long>("pulsecrm.events.published.total", description: "Events published to RabbitMQ");

    public static readonly ObservableGauge<int> SignalRConnections =
        Meter.CreateObservableGauge("pulsecrm.signalr.connections.active", () => 0); // Updated by hub

    public static IServiceCollection AddDealsTelemetry(this IServiceCollection services, IConfiguration configuration)
        => services.AddPulseCrmTelemetry(configuration, "pulsecrm-deals",
            configureMetrics: m => m.AddMeter("PulseCRM.Deals"));
}
