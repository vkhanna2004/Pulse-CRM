namespace PulseCRM.Shared.Contracts.Dtos;

public record StageDto(
    Guid Id,
    string Name,
    int Order,
    bool IsTerminal,
    IReadOnlyList<DealDto> Deals
);
