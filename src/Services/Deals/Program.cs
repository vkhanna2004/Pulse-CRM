using Microsoft.AspNetCore.Authentication.JwtBearer;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using PulseCRM.Deals.Consumers;
using PulseCRM.Deals.Features;
using PulseCRM.Deals.Grpc;
using PulseCRM.Deals.Hubs;
using PulseCRM.Deals.Infrastructure;
using Serilog;

// Required for gRPC over HTTP/2 cleartext inside Docker (no TLS between services)
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

// EF Core + Postgres
builder.Services.AddDbContext<DealsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DealsDb")));

// Tenant context
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// Auth
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var authority = builder.Configuration["Jwt:Authority"];
        options.Authority = authority;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MetadataAddress = $"{authority}/.well-known/openid-configuration";
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.ValidIssuers = BuildValidIssuers(authority);
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// SignalR + Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

// Event publisher helper
builder.Services.AddScoped<EventPublisher>();

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ScoringCompletedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]!);
            h.Password(builder.Configuration["RabbitMq:Password"]!);
        });
        // All deal events published to the shared topic exchange
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealCreated>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealMoved>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealUpdated>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealAssigned>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealActivityAdded>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealMentioned>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.ScoringCompleted>(m => m.SetEntityName("pulsecrm.events"));
        cfg.ConfigureEndpoints(ctx);
    });
});

// gRPC server (Deals exposes gRPC for internal use by Scoring/Notifications/Summarization)
builder.Services.AddGrpc();

// gRPC client to Scoring (for on-demand score queries)
builder.Services.AddGrpcClient<PulseCRM.Shared.Proto.Scoring.ScoringService.ScoringServiceClient>(options =>
    options.Address = new Uri(builder.Configuration["Services:ScoringGrpcUrl"]!));

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DealsDb")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddPrometheusExporter());

var app = builder.Build();

// Migrate + seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DealsDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

app.UseExceptionHandler("/error");

app.Map("/error", (HttpContext ctx) =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    return Results.Problem(title: "An error occurred", detail: null, statusCode: 500);
}).AllowAnonymous();

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

// gRPC service (internal — not proxied through gateway)
app.MapGrpcService<DealsGrpcService>();

// SignalR hub
app.MapHub<PipelineHub>("/hubs/pipeline");

// REST routes
app.MapDealRoutes();

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.MapPrometheusScrapingEndpoint();

app.Run();
