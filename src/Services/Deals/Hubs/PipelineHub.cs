using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PulseCRM.Deals.Hubs;

[Authorize]
public class PipelineHub : Hub
{

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinPipeline(string pipelineId)
    {
        var tenantId = GetTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:pipeline:{pipelineId}");
    }

    public async Task LeavePipeline(string pipelineId)
    {
        var tenantId = GetTenantId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:pipeline:{pipelineId}");
    }

    public async Task OpenDeal(string dealId, string pipelineId)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:deal:{dealId}");
        await Clients.Group($"tenant:{tenantId}:pipeline:{pipelineId}")
            .SendAsync("PresenceChanged", new { UserId = userId, Deals = new[] { dealId } });
    }

    public async Task CloseDeal(string dealId, string pipelineId)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:deal:{dealId}");
        await Clients.Group($"tenant:{tenantId}:pipeline:{pipelineId}")
            .SendAsync("PresenceChanged", new { UserId = userId, Deals = Array.Empty<string>() });
    }

    public async Task CursorMoved(double x, double y, string pipelineId)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        await Clients
            .GroupExcept($"tenant:{tenantId}:pipeline:{pipelineId}", Context.ConnectionId)
            .SendAsync("PresenceChanged", new { UserId = userId, X = x, Y = y });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        if (tenantId is not null && userId is not null)
        {
            // Notify all pipelines this user belongs to — send empty presence
            await Clients.Groups($"tenant:{tenantId}:pipeline:*")
                .SendAsync("PresenceChanged", new { UserId = userId, Deals = Array.Empty<string>() });
        }
        await base.OnDisconnectedAsync(exception);
    }

    private string? GetTenantId() => Context.User?.FindFirst("tenant_id")?.Value;
    private string? GetUserId() => Context.User?.FindFirst("sub")?.Value;
}
