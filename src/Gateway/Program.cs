using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authority = builder.Configuration["Jwt:Authority"];
        var requireHttpsMetadata = builder.Configuration.GetValue<bool?>("Jwt:RequireHttpsMetadata")
            ?? !builder.Environment.IsDevelopment();

        if (!string.IsNullOrWhiteSpace(authority) &&
            authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            requireHttpsMetadata = false;
        }

        options.Authority = authority;
        options.RequireHttpsMetadata = requireHttpsMetadata;
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
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Global rate limiter — enforced on all requests
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User?.FindFirst("sub")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 100),
            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimit:WindowSeconds", 60)),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    options.RejectionStatusCode = 429;
});

// CORS origins from config
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseExceptionHandler("/error");
app.Map("/error", () => Results.Problem(title: "An error occurred", statusCode: 500)).AllowAnonymous();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("X-Correlation-Id"))
        context.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-Id"] = context.Request.Headers["X-Correlation-Id"].ToString();
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();
app.MapGet("/healthz", () => Results.Ok("healthy")).AllowAnonymous();

app.Run();
