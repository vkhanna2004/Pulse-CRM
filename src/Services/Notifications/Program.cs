using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using PulseCRM.Notifications.Consumers;
using PulseCRM.Notifications.Hubs;
using PulseCRM.Notifications.Infrastructure;
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

builder.Services.AddScoped<NotificationsTenantContext>();
builder.Services.AddScoped<INotificationsTenantContext>(sp => sp.GetRequiredService<NotificationsTenantContext>());

builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")));

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

                // SignalR browser clients send bearer tokens via query string on WS/SSE transports.
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DealAssignedConsumer>();
    x.AddConsumer<DealMovedConsumer>();
    x.AddConsumer<DealMentionedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]!);
            h.Password(builder.Configuration["RabbitMq:Password"]!);
        });
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealAssigned>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealMoved>(m => m.SetEntityName("pulsecrm.events"));
        cfg.Message<PulseCRM.Shared.Contracts.Events.DealMentioned>(m => m.SetEntityName("pulsecrm.events"));
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("NotificationsDb")!);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddPrometheusExporter());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler("/error");
app.Map("/error", () => Results.Problem(title: "An error occurred", statusCode: 500)).AllowAnonymous();

app.UseAuthentication();

// Tenant middleware — sets NotificationsTenantContext per request
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            var tc = context.RequestServices.GetRequiredService<NotificationsTenantContext>();
            tc.Set(tenantId);
        }
    }
    await next();
});

app.UseAuthorization();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.MapGet("/api/notifications", async (NotificationsDbContext db, HttpContext ctx, bool? unreadOnly, int? limit) =>
{
    if (limit is < 1 or > 200)
        limit = 50;
    var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
    // Query filter handles tenant isolation automatically
    var query = db.Notifications.Where(n => n.UserId == userId);
    if (unreadOnly == true) query = query.Where(n => n.ReadAt == null);
    var results = await query.OrderByDescending(n => n.CreatedAt).Take(limit ?? 50).ToListAsync();
    return Results.Ok(results);
}).RequireAuthorization();

app.MapMethods("/api/notifications/{id:guid}/read", ["PATCH"], async (Guid id, NotificationsDbContext db, HttpContext ctx) =>
{
    var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
    // Query filter ensures tenant isolation; userId check prevents cross-user access
    var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (notification is null) return Results.NotFound();
    notification.ReadAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(notification);
}).RequireAuthorization();

app.MapPost("/api/notifications/mark-all-read", async (NotificationsDbContext db, HttpContext ctx) =>
{
    var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
    await db.Notifications
        .Where(n => n.UserId == userId && n.ReadAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow));
    return Results.Ok();
}).RequireAuthorization();

app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");
app.Run();
