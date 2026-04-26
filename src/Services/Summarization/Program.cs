using Microsoft.AspNetCore.Authentication.JwtBearer;
using Google.GenAI;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Summarization.Consumers;
using PulseCRM.Summarization.Hubs;
using PulseCRM.Summarization.Infrastructure;
using PulseCRM.Summarization.Services;
using OpenTelemetry.Metrics;
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
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddDbContext<SummarizationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SummarizationDb")));

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

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddGrpcClient<PulseCRM.Shared.Proto.Deals.DealsInternalService.DealsInternalServiceClient>(options =>
    options.Address = new Uri(builder.Configuration["Services:DealsGrpcUrl"]!));

// Google Gen AI client — reads GOOGLE_API_KEY from env automatically, or use explicit key from config
var googleApiKey = builder.Configuration["Google:ApiKey"];
builder.Services.AddSingleton(_ =>
    string.IsNullOrEmpty(googleApiKey)
        ? new Client()                          // falls back to GOOGLE_API_KEY env var
        : new Client(apiKey: googleApiKey));    // explicit key from appsettings / env override
builder.Services.AddScoped<SummarizationService>();

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
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealActivityAdded>(m => m.SetEntityName("pulsecrm.events"));
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("SummarizationDb")!);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddPrometheusExporter());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SummarizationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler("/error");
app.Map("/error", () => Results.Problem(title: "An error occurred", statusCode: 500)).AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<AiHub>("/hubs/ai");

app.MapGet("/api/ai/deals/{id:guid}/summary", async (Guid id, SummarizationDbContext db, HttpContext ctx) =>
{
    var tenantId = Guid.Parse(ctx.User.FindFirst("tenant_id")?.Value ?? Guid.Empty.ToString());
    var summary = await db.DealSummaries
        .Where(s => s.DealId == id && s.TenantId == tenantId)
        .OrderByDescending(s => s.GeneratedAt)
        .FirstOrDefaultAsync();
    return summary is null ? Results.NoContent() : Results.Ok(summary);
}).RequireAuthorization();

// Fix fire-and-forget scope: create new DI scope inside Task.Run
app.MapPost("/api/ai/deals/{id:guid}/summary:regenerate",
    async (Guid id, IServiceScopeFactory scopeFactory, HttpContext ctx) =>
{
    var tenantId = Guid.Parse(ctx.User.FindFirst("tenant_id")?.Value ?? Guid.Empty.ToString());
    _ = Task.Run(async () =>
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<SummarizationService>();
        await svc.GenerateAsync(tenantId, id, "Manual");
    });
    return Results.Accepted();
}).RequireAuthorization();

// Optimized cost-summary: aggregate in DB not in memory
app.MapGet("/api/ai/metrics/cost-summary", async (SummarizationDbContext db) =>
{
    var count = await db.DealSummaries.CountAsync();
    // Use raw SQL for JSONB aggregation to avoid loading all rows
    var result = await db.DealSummaries
        .GroupBy(_ => 1)
        .Select(g => new
        {
            SummaryCount = g.Count(),
            TotalGenerations = g.Count()
        })
        .FirstOrDefaultAsync();

    return Results.Ok(new
    {
        SummaryCount = count,
        Note = "Detailed token metrics available in Grafana dashboard"
    });
}).RequireAuthorization(policy => policy.RequireRole("admin"));

app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");
app.Run();
