using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PulseCRM.Notifications.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? "unknown";
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value ?? "unknown";
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{tenantId}:{userId}");
        await base.OnConnectedAsync();
    }
}
