using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using PulseCRM.Scoring.Consumers;
using PulseCRM.Scoring.Grpc;
using PulseCRM.Scoring.Infrastructure;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

static string[] BuildValidIssuers(string? authority)
{
    var issuers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http://localhost:8080/realms/pulsecrm",
        "http://keycloak:8080/realms/pulsecrm"
    };

    if (!string.IsNullOrWhiteSpace(authority))
    {
        var normalized = authority.TrimEnd('/');
        issuers.Add(normalized);

        if (normalized.Contains("keycloak", StringComparison.OrdinalIgnoreCase))
            issuers.Add(normalized.Replace("keycloak", "localhost", StringComparison.OrdinalIgnoreCase));
        if (normalized.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            issuers.Add(normalized.Replace("localhost", "keycloak", StringComparison.OrdinalIgnoreCase));
    }

    return issuers.ToArray();
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2);
    options.ListenAnyIP(8080, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddDbContext<ScoringDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ScoringDb")));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var authority = builder.Configuration["Jwt:Authority"];
        options.Authority = authority;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.ValidIssuers = BuildValidIssuers(authority);
    });
builder.Services.AddAuthorization();
builder.Services.AddGrpc();

// gRPC client for Deals (for snapshot callbacks)
builder.Services.AddGrpcClient<PulseCRM.Shared.Proto.Deals.DealsInternalService.DealsInternalServiceClient>(options =>
    options.Address = new Uri(builder.Configuration["Services:DealsGrpcUrl"]!));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DealEventConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]!);
            h.Password(builder.Configuration["RabbitMq:Password"]!);
        });
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealCreated>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealMoved>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealUpdated>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealActivityAdded>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.ScoringCompleted>(m => m.SetEntityName("pulsecrm.events"));
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHostedService<ConsumedEventCleanupJob>();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("ScoringDb")!);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddPrometheusExporter());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScoringDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler("/error");
app.Map("/error", () => Results.Problem(title: "An error occurred", statusCode: 500)).AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();
app.MapGrpcService<ScoringGrpcService>();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");
app.MapPrometheusScrapingEndpoint();
app.Run();
