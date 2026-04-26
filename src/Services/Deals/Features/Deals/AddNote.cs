using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Hubs;
using PulseCRM.Deals.Infrastructure;
using PulseCRM.Shared.Contracts.Dtos;
using System.Text.RegularExpressions;

namespace PulseCRM.Deals.Features.Deals;

public record AddNoteCommand(
    Guid DealId,
    string Content,
    Guid ActorUserId
) : IRequest<ActivityDto>;

public class AddNoteHandler : IRequestHandler<AddNoteCommand, ActivityDto>
{
    private static readonly Regex MentionRegex = new(@"@(\w+)", RegexOptions.Compiled);
    private readonly DealsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly EventPublisher _events;
    private readonly IHubContext<PipelineHub> _hub;

    public AddNoteHandler(DealsDbContext db, ITenantContext tenant, EventPublisher events, IHubContext<PipelineHub> hub)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _hub = hub;
    }

    public async Task<ActivityDto> Handle(AddNoteCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Content))
            throw new ArgumentException("Note content cannot be empty");
        if (cmd.Content.Length > 5000)
            throw new ArgumentException("Note content exceeds maximum length of 5000 characters");

        // Verify deal exists in this tenant
        var deal = await _db.Deals
            .Include(d => d.Stage).ThenInclude(s => s.Pipeline)
            .FirstOrDefaultAsync(d => d.Id == cmd.DealId, ct)
            ?? throw new KeyNotFoundException($"Deal {cmd.DealId} not found");

        // Resolve @mentions by matching against email local-part or first word of display name
        var mentionTokens = MentionRegex.Matches(cmd.Content)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToArray();

        Guid[] mentionedUserIds = [];
        if (mentionTokens.Length > 0)
        {
            mentionedUserIds = await _db.Users
                .Where(u => mentionTokens.Contains(
                    u.Email != null
                        ? u.Email.Split('@', StringSplitOptions.None)[0].ToLower()
                        : u.DisplayName.Split(' ', StringSplitOptions.None)[0].ToLower()))
                .Select(u => u.Id)
                .ToArrayAsync(ct);
        }

        var activity = new Activity
        {
            TenantId = _tenant.Current,
            DealId = cmd.DealId,
            ActorUserId = cmd.ActorUserId,
            Type = ActivityTypes.Note,
            Content = cmd.Content,
            MentionedUserIds = mentionedUserIds.Length > 0 ? mentionedUserIds : null
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);

        var actor = await _db.Users.FindAsync([cmd.ActorUserId], ct);
        var dto = activity.ToDto(actor?.DisplayName ?? "Unknown");

        // Broadcast activity added
        await _hub.Clients
            .Group($"tenant:{_tenant.Current}:pipeline:{deal.Stage.PipelineId}")
            .SendAsync("ActivityAdded", dto, ct);

        // Publish events
        await _events.PublishDealActivityAdded(cmd.DealId, ActivityTypes.Note, activity.Id, cmd.ActorUserId);

        if (mentionedUserIds.Length > 0)
            await _events.PublishDealMentioned(cmd.DealId, activity.Id, mentionedUserIds, cmd.ActorUserId);

        return dto;
    }
}
