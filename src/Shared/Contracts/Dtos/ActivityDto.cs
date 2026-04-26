namespace PulseCRM.Shared.Contracts.Dtos;

public record ActivityDto(
    Guid Id,
    Guid DealId,
    Guid ActorUserId,
    string ActorDisplayName,
    string Type,
    string? Content,
    Guid[]? MentionedUserIds,
    DateTimeOffset CreatedAt
);
