using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PulseCRM.Summarization.Hubs;

[Authorize]
public class AiHub : Hub
{
    public async Task WatchDeal(string dealId)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value ?? "unknown";
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:deal:{dealId}");
    }

    public async Task UnwatchDeal(string dealId)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value ?? "unknown";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:deal:{dealId}");
    }
}
