using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseCRM.Shared.Contracts.Telemetry;

namespace PulseCRM.Notifications.Infrastructure;

public static class NotificationsTelemetry
{
    public static readonly Meter Meter = new("PulseCRM.Notifications");

    public static readonly Counter<long> NotificationsDelivered =
        Meter.CreateCounter<long>("pulsecrm.notifications.delivered.total");

    public static readonly Counter<long> EnrichmentFailures =
        Meter.CreateCounter<long>("pulsecrm.notifications.enrichment_failures.total");

    public static IServiceCollection AddNotificationsTelemetry(this IServiceCollection services, IConfiguration configuration)
        => services.AddPulseCrmTelemetry(configuration, "pulsecrm-notifications",
            configureMetrics: m => m.AddMeter("PulseCRM.Notifications"));
}
