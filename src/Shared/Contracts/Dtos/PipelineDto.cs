namespace PulseCRM.Shared.Contracts.Dtos;

public record PipelineDto(
    Guid Id,
    string Name,
    IReadOnlyList<StageDto> Stages
);
