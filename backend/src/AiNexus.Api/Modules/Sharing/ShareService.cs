using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Artifacts;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Knowledge;
using AiNexus.Modules.WebSearch;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Sharing;

public sealed class ShareLink
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid SourceId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public bool IncludeAttachments { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class ShareRecipient { public Guid ShareId { get; set; } public Guid UserId { get; set; } }
public sealed record CreateShareRequest(string Kind, Guid SourceId, IReadOnlyList<Guid> RecipientIds, int Hours = 168, bool IncludeAttachments = false, int? ArtifactVersion = null);
public sealed record ShareDto(Guid Id, string Kind, string Title, string Owner, bool IsOwner, bool IsRevoked, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt, IReadOnlyList<string> Recipients, bool IncludeAttachments);
public sealed record SharedMessageDto(string Role, string Content, string Status, DateTimeOffset CreatedAt, IReadOnlyList<AttachmentDto> Attachments,
    string? ModelId = null, string? ErrorCode = null, IReadOnlyList<CitationDto>? Sources = null, IReadOnlyList<WebSourceDto>? WebSources = null, RunTimingDto? Timing = null, string? ModelDisplayName = null);
public sealed record SharedFilePreviewDto(AttachmentDto File, IReadOnlyList<DocumentPageDto> Pages);
public sealed record ShareSnapshot(string Content, int? ArtifactVersion, IReadOnlyList<SharedMessageDto> Messages);
public sealed record SharedContentDto(ShareDto Share, ShareSnapshot Snapshot);
public sealed class ShareWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }

public sealed class ShareService(NexusDbContext db, ResourceAccess access, AccessService features, AttachmentWriteLock attachmentWrites, ShareWriteLock writes, AiNexus.Modules.Inference.GenerationScheduler scheduler, AttachmentQuota quota, ModelPresentation presentation, AiNexus.Modules.Notifications.NotificationService notifications)
{
    public async Task<ShareDto> CreateAsync(Guid actor, CreateShareRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("conversation" or "artifact") || request.Hours is < 1 or > 720 || request.RecipientIds.Count is < 1 or > 20 || request.RecipientIds.Distinct().Count() != request.RecipientIds.Count || request.RecipientIds.Contains(actor)) throw new ApiException(400, "share_invalid", "請選擇 1 至 20 位其他使用者，分享期限介於 1 小時與 30 天。");
        if (await db.Users.CountAsync(x => request.RecipientIds.Contains(x.Id), ct) != request.RecipientIds.Count) throw new ApiException(400, "share_recipient_unknown", "只能分享給已登入過平台的使用者。");
        await writes.Gate.WaitAsync(ct);
        try
        {
            await scheduler.StateGate.WaitAsync(ct);
            try {
            await attachmentWrites.Gate.WaitAsync(ct);
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await quota.LockOwnerAsync(actor, ct);
                var now = DateTimeOffset.UtcNow;
                if (await db.Set<ShareLink>().CountAsync(x => x.OwnerId == actor && !x.IsRevoked && x.ExpiresAt > now, ct) >= 100) throw new ApiException(409, "share_limit", "有效分享已達 100 個，請撤銷不再使用的分享。");
                await RequireSourceFeature(actor, request.Kind, ct);
                var (title, snapshot, files) = await SnapshotAsync(actor, request, ct);
                var resource = new WorkspaceResource { OwnerId = actor, Kind = "share", Name = title };
                var share = new ShareLink { Id = resource.Id, OwnerId = actor, SourceId = request.SourceId, Kind = request.Kind, Title = title, IncludeAttachments = request.IncludeAttachments, ExpiresAt = now.AddHours(request.Hours), SnapshotJson = JsonSerializer.Serialize(snapshot) };
                db.Add(resource); db.Add(share); db.AddRange(request.RecipientIds.Select(id => new ShareRecipient { ShareId = share.Id, UserId = id }));
                db.AddRange(files.Select(file => new AttachmentReference { ResourceId = share.Id, AttachmentId = file }));
                db.AuditEvents.Add(new() { OwnerId = actor, Action = "share.created", ResourceId = share.Id, Result = "created", DetailsJson = JsonSerializer.Serialize(new { request.Kind, recipients = request.RecipientIds, share.ExpiresAt, request.IncludeAttachments }) });
                foreach (var recipient in request.RecipientIds)
                    await notifications.PublishAsync(recipient, "share:" + share.Id, "share.received", "info", "收到新的分享", title, "share", share.Id, ct);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await DescribeAsync(actor, share, ct);
            }
            finally { attachmentWrites.Gate.Release(); }
            } finally { scheduler.StateGate.Release(); }
        }
        finally { writes.Gate.Release(); }
    }
    private async Task<(string Title, ShareSnapshot Snapshot, Guid[] Files)> SnapshotAsync(Guid actor, CreateShareRequest request, CancellationToken ct)
    {
        if (request.Kind == "artifact")
        {
            var source = await access.OwnerAsync(actor, request.SourceId, "artifact", ct);
            var item = await db.Set<Artifact>().AsNoTracking().SingleAsync(x => x.Id == source.Id, ct);
            var version = request.ArtifactVersion ?? item.Version;
            var revision = await db.Set<ArtifactRevision>().AsNoTracking().SingleOrDefaultAsync(x => x.ArtifactId == item.Id && x.Version == version, ct) ?? throw Missing();
            return (revision.Title, new(revision.Content, revision.Version, []), []);
        }
        var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SourceId && x.OwnerId == actor && !x.IsDeleted, ct) ?? throw Missing();
        if (await db.Runs.AnyAsync(x => x.ConversationId == conversation.Id && x.ActiveOwnerId != null, ct)) throw new ApiException(409, "share_generation_active", "請先等待或停止回答，再分享完整內容。");
        var all = await db.Messages.AsNoTracking().Where(x => x.ConversationId == conversation.Id).ToListAsync(ct);
        var lookup = all.ToDictionary(x => x.Id); var branch = new List<Message>(); var visited = new HashSet<Guid>(); Guid? next = conversation.ActiveLeafId;
        while (next is Guid id)
        {
            if (!visited.Add(id) || !lookup.TryGetValue(id, out var message)) throw new ApiException(409, "share_history_invalid", "對話分支資料不完整。");
            branch.Add(message); next = message.ParentId;
            if (branch.Count > 100) throw new ApiException(409, "share_history_limit", "單次分享最多 100 則訊息，請改分享整理後的成果文件。");
        }
        if (branch.Sum(x => x.Content.Length) > 256000) throw new ApiException(409, "share_content_limit", "分享內容過長，請改分享整理後的成果文件。");
        branch.Reverse(); var ids = branch.Select(x => x.Id).ToArray();
        var links = request.IncludeAttachments ? await db.Set<MessageAttachment>().Where(x => ids.Contains(x.MessageId)).Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.Size }).ToListAsync(ct) : [];
        var citations = await db.Set<MessageCitation>().AsNoTracking().Where(x => ids.Contains(x.MessageId)).ToListAsync(ct);
        var runs = await db.Runs.AsNoTracking().Where(x => ids.Contains(x.AssistantMessageId)).ToDictionaryAsync(x => x.AssistantMessageId, ct);
        var runIds = runs.Values.Select(x => x.Id).ToArray();
        var searches = await db.Set<WebSearchRecord>().AsNoTracking().Where(x => x.RunId != null && runIds.Contains(x.RunId.Value)).ToListAsync(ct);
        var messages = branch.Select(x => new SharedMessageDto(x.Role, x.Content, x.Status, x.CreatedAt,
            links.Where(l => l.MessageId == x.Id).Select(l => new AttachmentDto(l.Id, l.FileName, l.ContentType, l.Size, l.ContentType.StartsWith("image/"), "shared-file")).ToArray(),
            x.ModelId, x.ErrorCode, citations.Where(c => c.MessageId == x.Id).OrderBy(c => c.Number).Select(c => new CitationDto(c.Number, c.DocumentId, c.Title, c.PageNumber, c.Excerpt)).ToArray(),
            runs.TryGetValue(x.Id, out var run) && searches.Any(s => s.RunId == run.Id) ? searches.Where(s => s.RunId == run.Id).SelectMany(WebSearchService.Sources).ToArray() : null,
            runs.TryGetValue(x.Id, out var timing) ? RunTiming.Describe(timing) : null)).ToArray();
        return (conversation.Title, new("", null, messages), links.Select(x => x.Id).Distinct().ToArray());
    }
    public async Task<IReadOnlyList<ShareDto>> ListAsync(Guid actor, bool sent, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Set<ShareLink>().AsNoTracking().Where(x => sent ? x.OwnerId == actor : !x.IsRevoked && x.ExpiresAt > now && db.Set<ShareRecipient>().Any(r => r.ShareId == x.Id && r.UserId == actor)).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var result = new List<ShareDto>(); foreach (var row in rows) { if (sent || await SourceExistsAsync(row, ct)) result.Add(await DescribeAsync(actor, row, ct)); } return result;
    }
    public async Task<SharedContentDto> ReadAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var share = await RequireAsync(actor, id, ct);
        var snapshot = JsonSerializer.Deserialize<ShareSnapshot>(share.SnapshotJson)!;
        return new(await DescribeAsync(actor, share, ct), snapshot with { Messages = snapshot.Messages.Select(x => x with { ModelId = x.ModelId is null ? null : presentation.PublicId(x.ModelId), ModelDisplayName = presentation.DisplayName(x.ModelId) }).ToArray() });
    }
    public async Task<Attachment> FileAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var share = await RequireAsync(actor, id, ct);
        if (!share.IncludeAttachments || !await db.Set<AttachmentReference>().AnyAsync(x => x.ResourceId == id && x.AttachmentId == fileId, ct)) throw Missing();
        var file = await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.StorageState == AttachmentStates.Ready, ct) ?? throw Missing();
        var metadata = JsonSerializer.Deserialize<ShareSnapshot>(share.SnapshotJson)!.Messages.SelectMany(x => x.Attachments).FirstOrDefault(x => x.Id == fileId) ?? throw Missing();
        file.FileName = metadata.FileName;
        return file;
    }
    public async Task<SharedFilePreviewDto> PreviewAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var file = await FileAsync(actor, id, fileId, ct);
        var documentId = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.AttachmentId == fileId && !x.IsDeleted && x.Status == "ready").OrderBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        IReadOnlyList<DocumentPageDto> pages = documentId is Guid doc ? await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == doc).OrderBy(x => x.PageNumber).Select(x => new DocumentPageDto(x.PageNumber, x.Text, x.Extraction, x.NeedsReview)).ToArrayAsync(ct)
            : string.IsNullOrWhiteSpace(file.ExtractedText) ? [] : [new DocumentPageDto(1, file.ExtractedText, "shared", false)];
        return new(AttachmentService.Describe(file), pages);
    }
    public async Task RevokeAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var row = await db.Set<ShareLink>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct) ?? throw Missing();
            row.IsRevoked = true; row.SnapshotJson = "";
            await db.Set<AttachmentReference>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "share.revoked", ResourceId = id, Result = "revoked" }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task RevokeSourceAsync(string kind, Guid id, CancellationToken ct)
    {
        var ids = db.Set<ShareLink>().Where(x => x.Kind == kind && x.SourceId == id).Select(x => x.Id);
        await db.Set<AttachmentReference>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await db.Set<ShareLink>().Where(x => x.Kind == kind && x.SourceId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsRevoked, true).SetProperty(x => x.SnapshotJson, ""), ct);
    }
    public async Task PurgeExpiredAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = db.Set<ShareLink>().Where(x => x.ExpiresAt <= now || x.IsRevoked).Select(x => x.Id);
        await db.Set<AttachmentReference>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await db.Set<ShareLink>().Where(x => x.ExpiresAt <= now && x.SnapshotJson != "").ExecuteUpdateAsync(p => p.SetProperty(x => x.SnapshotJson, ""), ct);
        await tx.CommitAsync(ct);
    }
    private async Task<ShareLink> RequireAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var row = await db.Set<ShareLink>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsRevoked && x.ExpiresAt > now && (x.OwnerId == actor || db.Set<ShareRecipient>().Any(r => r.ShareId == id && r.UserId == actor)), ct) ?? throw Missing();
        if (!await SourceExistsAsync(row, ct)) throw Missing();
        await RequireSourceFeature(row.OwnerId, row.Kind, ct); return row;
    }
    private async Task<bool> SourceExistsAsync(ShareLink row, CancellationToken ct) => row.Kind == "conversation" ? await db.Conversations.AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId && !x.IsDeleted, ct) : await db.Set<WorkspaceResource>().AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId && x.Kind == "artifact" && !x.IsDeleted, ct);
    private async Task RequireSourceFeature(Guid actor, string kind, CancellationToken ct) { if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == (kind == "conversation" ? "chat" : "artifacts"))) throw Missing(); }
    private async Task<ShareDto> DescribeAsync(Guid actor, ShareLink x, CancellationToken ct)
    {
        var owner = await db.Users.Where(u => u.Id == x.OwnerId).Select(u => u.DisplayName).SingleAsync(ct);
        // Only the sender sees the full recipient list.
        var recipients = actor == x.OwnerId ? await (from r in db.Set<ShareRecipient>() join u in db.Users on r.UserId equals u.Id where r.ShareId == x.Id select u.DisplayName).ToListAsync(ct) : [];
        return new(x.Id, x.Kind, x.Title, owner, x.OwnerId == actor, x.IsRevoked, x.ExpiresAt, x.CreatedAt, recipients, x.IncludeAttachments);
    }
    private static ApiException Missing() => new(404, "share_unavailable", "分享不存在、已到期或撤銷，或你的帳號不在收件者清單中。");
}
public static class SharingConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var link = model.Entity<ShareLink>(); link.ToTable("ShareLinks", "collaboration"); link.HasKey(x => x.Id); link.Property(x => x.Kind).HasMaxLength(24); link.Property(x => x.Title).HasMaxLength(120); link.HasIndex(x => new { x.OwnerId, x.CreatedAt }); link.HasIndex(x => x.ExpiresAt); link.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict); link.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var recipient = model.Entity<ShareRecipient>(); recipient.ToTable("ShareRecipients", "collaboration"); recipient.HasKey(x => new { x.ShareId, x.UserId }); recipient.HasIndex(x => new { x.UserId, x.ShareId }); recipient.HasOne<ShareLink>().WithMany().HasForeignKey(x => x.ShareId).OnDelete(DeleteBehavior.Cascade); recipient.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ShareCleanupWorker(IServiceScopeFactory scopes, StorageReadiness storage, ILogger<ShareCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!storage.Configured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(20));
        do { try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ShareService>().PurgeExpiredAsync(ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; } catch (Exception ex) { logger.LogWarning("Share cleanup deferred ({ErrorType}).", ex.GetType().Name); } } while (await timer.WaitForNextTickAsync(ct));
    }
}
