using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PulseCRM.Shared.Contracts.Telemetry;

public static class OtelExtensions
{
    public static IServiceCollection AddPulseCrmTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var otlpEndpoint = configuration["Otel:Endpoint"] ?? "http://otel-collector:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: "1.0.0")
                .AddAttributes([new("deployment.environment", "docker")]))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddEntityFrameworkCoreInstrumentation()
                 .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                configureTracing?.Invoke(t);
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddRuntimeInstrumentation()
                 .AddPrometheusExporter()
                 .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                configureMetrics?.Invoke(m);
            })
            .WithLogging(l => l.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }
}
