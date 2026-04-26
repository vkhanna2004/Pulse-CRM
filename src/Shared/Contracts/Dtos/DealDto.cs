namespace PulseCRM.Shared.Contracts.Dtos;

public record DealDto(
    Guid Id,
    Guid TenantId,
    string Title,
    decimal Value,
    string Currency,
    Guid StageId,
    int PositionInStage,
    Guid OwnerId,
    string OwnerDisplayName,
    Guid? ContactId,
    string? ContactDisplayName,
    int Score,
    DateTimeOffset? ScoreCalculatedAt,
    bool IsClosed,
    DateTimeOffset? ExpectedCloseDate,
    uint RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
