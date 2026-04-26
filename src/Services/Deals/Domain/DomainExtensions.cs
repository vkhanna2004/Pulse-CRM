using PulseCRM.Shared.Contracts.Dtos;

namespace PulseCRM.Deals.Domain;

public static class DomainExtensions
{
    public static DealDto ToDto(this Deal d) => new(
        d.Id, d.TenantId, d.Title, d.Value, d.Currency,
        d.StageId, d.PositionInStage,
        d.OwnerId, d.Owner?.DisplayName ?? "",
        d.ContactId, d.Contact?.Name,
        d.Score, d.ScoreCalculatedAt, d.IsClosed,
        d.ExpectedCloseDate, d.RowVersion,
        d.CreatedAt, d.UpdatedAt
    );

    public static ActivityDto ToDto(this Activity a, string actorDisplayName) => new(
        a.Id, a.DealId, a.ActorUserId, actorDisplayName,
        a.Type, a.Content, a.MentionedUserIds, a.CreatedAt
    );
}
