using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Notifications.Domain;
using PulseCRM.Notifications.Hubs;
using PulseCRM.Notifications.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;
using PulseCRM.Shared.Contracts.Events;
using PulseCRM.Shared.Proto.Deals;

namespace PulseCRM.Notifications.Consumers;

public class DealMovedConsumer : IConsumer<DealMoved>
{
    private readonly NotificationsDbContext _db;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;
    private readonly IHubContext<NotificationsHub> _hub;

    public DealMovedConsumer(NotificationsDbContext db, DealsInternalService.DealsInternalServiceClient dealsClient, IHubContext<NotificationsHub> hub)
    {
        _db = db;
        _dealsClient = dealsClient;
        _hub = hub;
    }

    public async Task Consume(ConsumeContext<DealMoved> context)
    {
        var msg = context.Message;

        // Always insert dedup first to prevent infinite retry on Deals outage
        _db.ConsumedEvents.Add(new ConsumedEvent { TenantId = msg.TenantId, EventId = msg.EventId });
        try { await _db.SaveChangesAsync(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return; } // Duplicate — already processed

        string message;
        Guid targetUserId;
        var deepLink = $"/deals/{msg.DealId}";

        try
        {
            var deal = await _dealsClient.GetDealContextAsync(new GetDealRequest
            {
                TenantId = msg.TenantId.ToString(),
                DealId = msg.DealId.ToString()
            });

            // OwnerId is now in the proto response (owner_id field)
            if (!Guid.TryParse(deal.OwnerId, out var ownerId)) return;
            if (ownerId == msg.ActorUserId) return; // No self-notifications

            targetUserId = ownerId;
            message = $"\"{deal.Title}\" was moved to {deal.StageName}";
        }
        catch
        {
            // Enrichment failed — skip notification for DealMoved (not critical enough to degrade)
            return;
        }

        var notification = new Notification
        {
            TenantId = msg.TenantId,
            UserId = targetUserId,
            Type = "DealStageChanged",
            Message = message,
            DeepLink = deepLink,
            EnrichmentFailed = false
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group($"user:{msg.TenantId}:{targetUserId}")
            .SendAsync("NotificationReceived", new NotificationDto(
                notification.Id, notification.UserId, notification.Type,
                notification.Message, notification.DeepLink,
                notification.EnrichmentFailed, notification.ReadAt, notification.CreatedAt));
    }
}
