using MassTransit;
using PulseCRM.Shared.Contracts.Events;

namespace PulseCRM.Deals.Infrastructure;

public class EventPublisher
{
    private readonly IPublishEndpoint _bus;
    private readonly ITenantContext _tenant;

    public EventPublisher(IPublishEndpoint bus, ITenantContext tenant)
    {
        _bus = bus;
        _tenant = tenant;
    }

    public Task PublishDealCreated(Guid dealId, Guid stageId, Guid ownerId, decimal value, Guid? contactId, Guid actorUserId) =>
        _bus.Publish(new DealCreated
        {
            EventType = "DealCreated", TenantId = _tenant.Current, ActorUserId = actorUserId,
            DealId = dealId, StageId = stageId, OwnerId = ownerId, Value = value, ContactId = contactId
        });

    public Task PublishDealMoved(Guid dealId, Guid fromStageId, Guid toStageId, int positionInStage, Guid actorUserId) =>
        _bus.Publish(new DealMoved
        {
            EventType = "DealMoved", TenantId = _tenant.Current, ActorUserId = actorUserId,
            DealId = dealId, FromStageId = fromStageId, ToStageId = toStageId, PositionInStage = positionInStage
        });

    public Task PublishDealUpdated(Guid dealId, IReadOnlyList<string> changedFields, Guid actorUserId) =>
        _bus.Publish(new DealUpdated
        {
            EventType = "DealUpdated", TenantId = _tenant.Current, ActorUserId = actorUserId,
            DealId = dealId, ChangedFields = changedFields
        });

    public Task PublishDealAssigned(Guid dealId, Guid? previousOwnerId, Guid newOwnerId, Guid actorUserId) =>
        _bus.Publish(new DealAssigned
        {
            EventType = "DealAssigned", TenantId = _tenant.Current, ActorUserId = actorUserId,
            DealId = dealId, PreviousOwnerId = previousOwnerId, NewOwnerId = newOwnerId
        });

    public Task PublishDealActivityAdded(Guid dealId, string activityType, Guid activityId, Guid actorUserId) =>
        _bus.Publish(new DealActivityAdded
        {
            EventType = "DealActivityAdded", TenantId = _tenant.Current, ActorUserId = actorUserId,
            DealId = dealId, ActivityType = activityType, ActivityId = activityId
        });

    public Task PublishDealMentioned(Guid dealId, Guid noteActivityId, IReadOnlyList<Guid> mentionedUserIds, Guid actorUserId) =>
        _bus.Publish(new DealMentioned
        {
            EventType = "DealMentioned", TenantId = _tenant.Current, ActorUserId = actorUserId,
            DealId = dealId, NoteActivityId = noteActivityId, MentionedUserIds = mentionedUserIds
        });
}
