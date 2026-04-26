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

public class DealAssignedConsumer : IConsumer<DealAssigned>
{
    private readonly NotificationsDbContext _db;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;
    private readonly IHubContext<NotificationsHub> _hub;

    public DealAssignedConsumer(NotificationsDbContext db, DealsInternalService.DealsInternalServiceClient dealsClient, IHubContext<NotificationsHub> hub)
    {
        _db = db;
        _dealsClient = dealsClient;
        _hub = hub;
    }

    public async Task Consume(ConsumeContext<DealAssigned> context)
    {
        var msg = context.Message;
        if (await _db.ConsumedEvents.AnyAsync(e => e.TenantId == msg.TenantId && e.EventId == msg.EventId)) return;

        string message;
        var deepLink = $"/deals/{msg.DealId}";
        bool enrichmentFailed = false;

        try
        {
            var deal = await _dealsClient.GetDealContextAsync(new GetDealRequest
            {
                TenantId = msg.TenantId.ToString(),
                DealId = msg.DealId.ToString()
            });
            message = $"You were assigned \"{deal.Title}\" (${deal.Value:N0})";
        }
        catch
        {
            message = "A deal was assigned to you";
            enrichmentFailed = true;
        }

        var notification = new Notification
        {
            TenantId = msg.TenantId,
            UserId = msg.NewOwnerId,
            Type = "DealAssigned",
            Message = message,
            DeepLink = deepLink,
            EnrichmentFailed = enrichmentFailed
        };

        _db.Notifications.Add(notification);
        _db.ConsumedEvents.Add(new ConsumedEvent { TenantId = msg.TenantId, EventId = msg.EventId });
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group($"user:{msg.TenantId}:{msg.NewOwnerId}")
            .SendAsync("NotificationReceived", new NotificationDto(
                notification.Id, notification.UserId, notification.Type,
                notification.Message, notification.DeepLink,
                notification.EnrichmentFailed, notification.ReadAt, notification.CreatedAt));
    }
}
