using Microsoft.EntityFrameworkCore;

namespace PulseCRM.Deals.Infrastructure;

public interface ITenantContext
{
    Guid Current { get; }
    Guid CurrentUserId { get; }
}

public class TenantContext : ITenantContext
{
    public Guid Current { get; private set; }
    public Guid CurrentUserId { get; private set; }

    public void Set(Guid tenantId, Guid userId)
    {
        Current = tenantId;
        CurrentUserId = userId;
    }
}

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, DealsDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            var subClaim = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(tenantClaim, out var tenantId) || !Guid.TryParse(subClaim, out var userId))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Missing required claims");
                return;
            }

            tenantContext.Set(tenantId, userId);

            // JIT user upsert - create/update user on first authenticated request
            var existingUser = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.KeycloakSub == subClaim);

            if (existingUser is null)
            {
                var displayName = context.User.FindFirst("name")?.Value
                    ?? context.User.FindFirst("preferred_username")?.Value
                    ?? subClaim;
                var email = context.User.FindFirst("email")?.Value;

                db.Users.Add(new Domain.AppUser
                {
                    Id = userId,
                    TenantId = tenantId,
                    KeycloakSub = subClaim,
                    DisplayName = displayName,
                    Email = email
                });
                try { await db.SaveChangesAsync(); }
                catch { /* ignore race condition duplicate insert */ }
            }
        }

        await _next(context);
    }
}
