using System.Text.Json;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Notifications;
using AiNexus.Features.Persistence;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Sharing;

public sealed record SharedMessageDto(string Role, string Content, string Status, DateTimeOffset CreatedAt, IReadOnlyList<AttachmentDto> Attachments,
    string? ModelId = null, string? ErrorCode = null, IReadOnlyList<CitationDto>? Sources = null, IReadOnlyList<WebSourceDto>? WebSources = null, RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);

public sealed record CreateShareRequest(string Kind, Guid SourceId, IReadOnlyList<Guid> RecipientIds, int Hours = 168, bool IncludeAttachments = false, int? ArtifactVersion = null);

internal sealed class CreateShareRequestValidator : RequestValidator<CreateShareRequest>
{
    public override string ProblemCode => SharingErrors.InvalidCode;

    // The /api/v1 group resolves the current user before validation runs.
    public CreateShareRequestValidator(ICurrentUser user)
    {
        RuleFor(x => x.Kind).Must(x => x is "conversation" or "artifact").WithErrorCode("unknown");
        RuleFor(x => x.Hours).InclusiveBetween(1, ShareLink.MaxHours);
        RuleFor(x => x.RecipientIds).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.Count is >= 1 and <= ShareLink.MaxRecipients).WithErrorCode("count")
            .Must(x => x.Distinct().Count() == x.Count).WithErrorCode("duplicate")
            .Must(x => !x.Contains(user.Id)).WithErrorCode("self");
    }
}

/// <summary>Freezes a conversation branch or an artifact version for named recipients and notifies them.</summary>
internal sealed class CreateShare(NexusDbContext db, ResourceAccess access, ShareAccess shares, AttachmentWriteLock attachmentWrites, ShareWriteLock writes,
    GenerationScheduler scheduler, AttachmentQuota quota, NotificationService notifications, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", async (CreateShareRequest body, ICurrentUser user, CreateShare handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, body, ct)).ToHttpResult())
        .Produces<ShareDto>();

    private sealed record SourceSnapshot(string Title, ShareSnapshot Snapshot, Guid[] Files);

    public async Task<Result<ShareDto>> HandleAsync(Guid actor, CreateShareRequest request, CancellationToken ct)
    {
        if (await db.Users.CountAsync(x => request.RecipientIds.Contains(x.Id), ct) != request.RecipientIds.Count) return SharingErrors.RecipientUnknown;
        await writes.Gate.WaitAsync(ct);
        try
        {
            var conversationLock = await scheduler.LockConversationAsync(request.SourceId, ct);
            try
            {
                await attachmentWrites.Gate.WaitAsync(ct);
                try
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    await quota.LockOwnerAsync(actor, ct);
                    var now = clock.GetUtcNow();
                    if (await db.Set<ShareLink>().CountAsync(x => x.OwnerId == actor && !x.IsRevoked && x.ExpiresAt > now, ct) >= ShareLink.MaxActivePerOwner) return SharingErrors.LimitReached;
                    if (!await shares.HasSourceFeatureAsync(actor, request.Kind, ct)) return SharingErrors.Unavailable;
                    var snapshot = await SnapshotAsync(actor, request, ct);
                    if (!snapshot.IsSuccess) return snapshot.Error;
                    var (title, content, files) = snapshot.Value;
                    var resource = new WorkspaceResource { OwnerId = actor, Kind = "share", Name = title };
                    var share = new ShareLink { Id = resource.Id, OwnerId = actor, SourceId = request.SourceId, Kind = request.Kind, Title = title, IncludeAttachments = request.IncludeAttachments, CreatedAt = now, ExpiresAt = now.AddHours(request.Hours), SnapshotJson = JsonSerializer.Serialize(content) };
                    db.Add(resource); db.Add(share); db.AddRange(request.RecipientIds.Select(id => new ShareRecipient { ShareId = share.Id, UserId = id }));
                    db.AddRange(files.Select(file => new AttachmentReference { ResourceId = share.Id, AttachmentId = file }));
                    db.AuditEvents.Add(new() { OwnerId = actor, Action = "share.created", ResourceId = share.Id, Result = "created", DetailsJson = JsonSerializer.Serialize(new { request.Kind, recipients = request.RecipientIds, share.ExpiresAt, request.IncludeAttachments }) });
                    foreach (var recipient in request.RecipientIds)
                        await notifications.PublishAsync(recipient, "share:" + share.Id, "share.received", "info", "收到新的分享", title, "share", share.Id, ct);
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    return await shares.DescribeAsync(actor, share, ct);
                }
                finally { attachmentWrites.Gate.Release(); }
            }
            finally { conversationLock.Dispose(); }
        }
        finally { writes.Gate.Release(); }
    }

    private async Task<Result<SourceSnapshot>> SnapshotAsync(Guid actor, CreateShareRequest request, CancellationToken ct)
    {
        if (request.Kind == "artifact")
        {
            var source = (await access.OwnerAsync(actor, request.SourceId, "artifact", ct)).OrThrow();
            var item = await db.Set<Artifact>().AsNoTracking().SingleAsync(x => x.Id == source.Id, ct);
            var version = request.ArtifactVersion ?? item.Version;
            var revision = await db.Set<ArtifactRevision>().AsNoTracking().SingleOrDefaultAsync(x => x.ArtifactId == item.Id && x.Version == version, ct);
            if (revision is null) return SharingErrors.Unavailable;
            return new SourceSnapshot(revision.Title, new(revision.Content, revision.Version, []), []);
        }
        var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SourceId && x.OwnerId == actor, ct);
        if (conversation is null) return SharingErrors.Unavailable;
        if (await db.Runs.AnyAsync(x => x.ConversationId == conversation.Id && x.ActiveOwnerId != null, ct)) return SharingErrors.GenerationActive;
        var all = await db.Messages.AsNoTracking().Where(x => x.ConversationId == conversation.Id).ToListAsync(ct);
        var lookup = all.ToDictionary(x => x.Id); var branch = new List<Message>(); var visited = new HashSet<Guid>(); Guid? next = conversation.ActiveLeafId;
        while (next is Guid id)
        {
            if (!visited.Add(id) || !lookup.TryGetValue(id, out var message)) return SharingErrors.HistoryInvalid;
            branch.Add(message); next = message.ParentId;
            if (branch.Count > ShareLink.MaxMessages) return SharingErrors.HistoryLimit;
        }
        if (branch.Sum(x => x.Content.Length) > ShareLink.MaxContentCharacters) return SharingErrors.ContentLimit;
        branch.Reverse(); var ids = branch.Select(x => x.Id).ToArray();
        var links = request.IncludeAttachments ? await db.Set<MessageAttachment>().Where(x => ids.Contains(x.MessageId)).Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.Size }).ToListAsync(ct) : [];
        var citations = await db.Set<MessageCitation>().AsNoTracking().Where(x => ids.Contains(x.MessageId)).ToListAsync(ct);
        var runs = await db.Runs.AsNoTracking().Where(x => ids.Contains(x.AssistantMessageId)).ToDictionaryAsync(x => x.AssistantMessageId, ct);
        var runIds = runs.Values.Select(x => x.Id).ToArray();
        var searches = await db.Set<WebSearchRecord>().AsNoTracking().Where(x => x.RunId != null && runIds.Contains(x.RunId.Value)).ToListAsync(ct);
        var messages = branch.Select(x => new SharedMessageDto(x.Role, x.Content, x.Status, x.CreatedAt,
            links.Where(l => l.MessageId == x.Id).Select(l => new AttachmentDto(l.Id, l.FileName, l.ContentType, l.Size, l.ContentType.StartsWith("image/", StringComparison.Ordinal), "shared-file")).ToArray(),
            x.ModelId, x.ErrorCode, citations.Where(c => c.MessageId == x.Id).OrderBy(c => c.Number).Select(c => new CitationDto(c.Number, c.DocumentId, c.Title, c.PageNumber, c.Excerpt, c.EndPage)).ToArray(),
            runs.TryGetValue(x.Id, out var run) && searches.Any(s => s.RunId == run.Id) ? searches.Where(s => s.RunId == run.Id).SelectMany(WebSearchService.Sources).ToArray() : null,
            runs.TryGetValue(x.Id, out var timing) ? RunTiming.Describe(timing) : null, IssueCode: x.IssueCode)).ToArray();
        return new SourceSnapshot(conversation.Title, new("", null, messages), links.Select(x => x.Id).Distinct().ToArray());
    }
}
