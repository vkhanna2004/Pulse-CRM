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

public class DealMentionedConsumer : IConsumer<DealMentioned>
{
    private readonly NotificationsDbContext _db;
    private readonly DealsInternalService.DealsInternalServiceClient _dealsClient;
    private readonly IHubContext<NotificationsHub> _hub;

    public DealMentionedConsumer(NotificationsDbContext db, DealsInternalService.DealsInternalServiceClient dealsClient, IHubContext<NotificationsHub> hub)
    {
        _db = db;
        _dealsClient = dealsClient;
        _hub = hub;
    }

    public async Task Consume(ConsumeContext<DealMentioned> context)
    {
        var msg = context.Message;

        _db.ConsumedEvents.Add(new ConsumedEvent { TenantId = msg.TenantId, EventId = msg.EventId });
        try { await _db.SaveChangesAsync(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return; }

        string dealTitle;
        bool enrichmentFailed = false;

        try
        {
            var deal = await _dealsClient.GetDealContextAsync(new GetDealRequest
            {
                TenantId = msg.TenantId.ToString(),
                DealId = msg.DealId.ToString()
            });
            dealTitle = deal.Title;
        }
        catch
        {
            dealTitle = "a deal";
            enrichmentFailed = true;
        }

        var notifications = msg.MentionedUserIds.Select(userId => new Notification
        {
            TenantId = msg.TenantId,
            UserId = userId,
            Type = "MentionedInNote",
            Message = $"You were mentioned in a note on \"{dealTitle}\"",
            DeepLink = $"/deals/{msg.DealId}",
            EnrichmentFailed = enrichmentFailed
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync(); // Save ALL before any SignalR push

        // Push after successful save
        foreach (var notification in notifications)
        {
            await _hub.Clients
                .Group($"user:{msg.TenantId}:{notification.UserId}")
                .SendAsync("NotificationReceived", new NotificationDto(
                    notification.Id, notification.UserId, notification.Type,
                    notification.Message, notification.DeepLink,
                    notification.EnrichmentFailed, notification.ReadAt, notification.CreatedAt));
        }
    }
}
