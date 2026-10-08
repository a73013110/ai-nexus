using AiNexus.Features.Persistence;
using AiNexus.Features.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Collaboration;

namespace AiNexus.Features.Attachments;

/// <summary>Deleting is a durable outbox state. Metadata and quota survive until physical deletion succeeds.</summary>
public sealed class AttachmentLifecycle(NexusDbContext db, IAttachmentStorage storage, AttachmentQuota quota, IOptions<AttachmentOptions> options, ILogger<AttachmentLifecycle> logger)
{
    private IQueryable<Attachment> Unreferenced() => db.Set<Attachment>().Where(x =>
        !db.Set<MessageAttachment>().Any(l => l.AttachmentId == x.Id) && !db.Set<AttachmentReference>().Any(l => l.AttachmentId == x.Id));

    public async Task<int> MarkUnusedAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Attachment reference removal requires a transaction.");
        var files = ids.Distinct().ToArray();
        // A collaborator may remove a reader owned by another user. Lock the original's owner,
        // matching every reference-creation path, before deciding that its bytes can be deleted.
        var owners = await db.Set<Attachment>().Where(x => files.Contains(x.Id) && !x.InLibrary).Select(x => x.OwnerId).Distinct().Order().ToListAsync(ct);
        foreach (var owner in owners) await quota.LockOwnerAsync(owner, ct);
        return await Unreferenced().Where(x => files.Contains(x.Id) && !x.InLibrary)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.StorageState, AttachmentStates.Deleting), ct);
    }

    public IQueryable<Guid> PrivateReaders(Guid owner) => from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>() on doc.Id equals resource.Id
        where doc.CollectionId == null && resource.ParentId == null && resource.OwnerId == owner select doc.Id;

    public async Task RemovePrivateReadersAsync(Guid owner, IReadOnlyList<Guid> files, CancellationToken ct)
    {
        var ids = await db.Set<KnowledgeDocument>().Where(x => files.Contains(x.AttachmentId ?? Guid.Empty) && PrivateReaders(owner).Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        await db.Set<KnowledgeDocument>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.Status, "deleted").SetProperty(x => x.AttachmentId, (Guid?)null), ct);
        await db.Set<WorkspaceResource>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
        await db.Set<BackgroundJob>().Where(x => ids.Contains(x.SubjectId) && x.ActiveKey != null).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
        await db.Set<KnowledgeChunk>().Where(x => ids.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
        await db.Set<DocumentPage>().Where(x => ids.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
        await db.Set<AttachmentReference>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
    }

    private IQueryable<Attachment> ExpiredDrafts(DateTimeOffset cutoff, DateTimeOffset interrupted) => db.Set<Attachment>().Where(x => !x.InLibrary &&
        (x.StorageState == AttachmentStates.Ready && x.CreatedAt < cutoff || x.StorageState == AttachmentStates.Pending && x.CreatedAt < interrupted) &&
        !db.Set<MessageAttachment>().Any(l => l.AttachmentId == x.Id) &&
        !db.Set<AttachmentReference>().Any(l => l.AttachmentId == x.Id && !(from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>() on doc.Id equals resource.Id
            where doc.Id == l.ResourceId && doc.CollectionId == null && resource.ParentId == null && resource.OwnerId == x.OwnerId select doc.Id).Any()));

    public async Task AbortUploadAsync(Attachment upload, CancellationToken ct)
    {
        Guid cleanupId;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await quota.LockOwnerAsync(upload.OwnerId, ct);
            var state = await db.Set<Attachment>().Where(x => x.Id == upload.Id).Select(x => x.StorageState).SingleOrDefaultAsync(ct);
            // Activation may have committed even if its acknowledgement was lost.
            if (state == AttachmentStates.Ready) return;
            // An expired reservation may already have been reclaimed before its writer resumes.
            // Give cleanup a new row identity, so an earlier collector cannot delete this outbox
            // after it has checked the disk but before the late write actually reaches the disk.
            await db.Set<Attachment>().Where(x => x.Id == upload.Id && x.StorageState != AttachmentStates.Ready).ExecuteDeleteAsync(ct);
            db.Entry(upload).State = EntityState.Detached;
            var cleanup = new Attachment { OwnerId = upload.OwnerId, FileName = upload.FileName, ContentType = upload.ContentType,
                Size = upload.Size, StorageKey = upload.StorageKey, StorageState = AttachmentStates.Deleting };
            db.Add(cleanup);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            cleanupId = cleanup.Id;
        }
        await DeletePendingAsync(ct, cleanupId);
    }

    public async Task DeletePendingAsync(CancellationToken ct, Guid? id = null)
    {
        var rows = await db.Set<Attachment>().AsNoTracking().Where(x => x.StorageState == AttachmentStates.Deleting && (id == null || x.Id == id))
            .OrderBy(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.StorageKey }).ToListAsync(ct);
        foreach (var file in rows)
        {
            try
            {
                await storage.DeleteAsync(file.StorageKey, ct);
                await db.Set<Attachment>().Where(x => x.Id == file.Id && x.StorageState == AttachmentStates.Deleting).ExecuteDeleteAsync(ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { logger.LogWarning("Attachment {AttachmentId} deletion awaits retry ({ErrorType}).", file.Id, ex.GetType().Name); }
        }
    }

    public async Task ReclaimAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.DraftRetentionDays);
        var interrupted = DateTimeOffset.UtcNow.AddHours(-1);
        var candidates = await ExpiredDrafts(cutoff, interrupted).AsNoTracking()
            .OrderBy(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.OwnerId }).ToListAsync(ct);
        foreach (var owner in candidates.GroupBy(x => x.OwnerId))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(owner.Key, ct);
            var ids = owner.Select(x => x.Id).ToArray();
            var expired = await ExpiredDrafts(cutoff, interrupted).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
            await RemovePrivateReadersAsync(owner.Key, expired, ct);
            await MarkUnusedAsync(expired, ct);
            await transaction.CommitAsync(ct);
        }
        await DeletePendingAsync(ct);
    }

    // Reconcile only in the scheduled worker, not on each upload. A grace period protects
    // recent writes; normal uploads commit their SQL reservation before any file appears.
    public async Task ReconcileAsync(CancellationToken ct)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var key in storage.StaleKeysAsync(DateTimeOffset.UtcNow.AddDays(-1), ct))
        {
            keys.Add(key);
            if (keys.Count < 100) continue;
            await RemoveUntrackedAsync(keys, ct);
            keys.Clear();
        }
        if (keys.Count > 0) await RemoveUntrackedAsync(keys, ct);
    }

    private async Task RemoveUntrackedAsync(IReadOnlySet<string> keys, CancellationToken ct)
    {
        var tracked = await db.Set<Attachment>().Where(x => keys.Contains(x.StorageKey)).Select(x => x.StorageKey).ToHashSetAsync(ct);
        foreach (var key in keys.Where(x => !tracked.Contains(x)))
        {
            try { await storage.DeleteAsync(key, ct); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { logger.LogWarning("Untracked attachment {StorageKey} deletion awaits retry ({ErrorType}).", key, ex.GetType().Name); }
        }
    }
}

public sealed class AttachmentCleanupWorker(IServiceScopeFactory scopes, IOptions<AttachmentOptions> options, StorageReadiness readiness, ILogger<AttachmentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!readiness.Configured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>();
                await lifecycle.ReclaimAsync(stoppingToken);
                await lifecycle.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning("Attachment cleanup awaits retry ({ErrorType}).", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
