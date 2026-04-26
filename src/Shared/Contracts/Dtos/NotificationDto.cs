namespace PulseCRM.Shared.Contracts.Dtos;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Message,
    string DeepLink,
    bool EnrichmentFailed,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt
);
