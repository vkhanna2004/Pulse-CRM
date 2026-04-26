namespace PulseCRM.Shared.Contracts.Events;
public record DealMentioned : EventEnvelope
{
    public required Guid DealId { get; init; }
    public required Guid NoteActivityId { get; init; }
    public required IReadOnlyList<Guid> MentionedUserIds { get; init; }
}
