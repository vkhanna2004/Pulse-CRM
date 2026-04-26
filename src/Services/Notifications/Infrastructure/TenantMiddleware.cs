using System.Security.Claims;

namespace PulseCRM.Notifications.Infrastructure;

public class TenantContext
{
    public Guid TenantId { get; set; }
}

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            tenantContext.TenantId = tenantId;
        }

        await _next(context);
    }
}
