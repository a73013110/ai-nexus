using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Files.Storage;

/// <summary>刪除是持久化的 outbox 狀態：中繼資料與配額要保留到實體檔案真的刪除為止。</summary>
[Scoped]
public sealed partial class AttachmentLifecycle(AppDbContext db, IAttachmentStorage storage, AttachmentQuota quota, IPrivateReaders readers, IOptions<FilesOptions> options, ILogger<AttachmentLifecycle> logger, TimeProvider clock)
{
    private IQueryable<Attachment> Unreferenced() => db.Attachments.Where(x =>
        !db.MessageAttachments.Any(l => l.AttachmentId == x.Id) && !db.ResourceAttachments.Any(l => l.AttachmentId == x.Id));

    public async Task<int> MarkUnusedAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Attachment reference removal requires a transaction.");
        var files = ids.Distinct().ToArray();
        // 協作者可以移除別人擁有的讀取器，所以要鎖原始檔案的擁有者（與所有建立引用的路徑一致），
        // 才能判斷位元組可以刪除。
        var owners = await db.Attachments.Where(x => files.Contains(x.Id) && !x.InLibrary).Select(x => x.OwnerId).Distinct().Order().ToListAsync(ct);
        foreach (var owner in owners) await quota.LockOwnerAsync(owner, ct);
        return await Unreferenced().Where(x => files.Contains(x.Id) && !x.InLibrary)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.StorageState, AttachmentStates.Deleting), ct);
    }

    /// <summary>
    /// 鎖住這些檔案的擁有者，順序固定。要解除引用的呼叫端在改文件之前先呼叫：
    /// 其他路徑都是先鎖擁有者再改文件（如 <see cref="RemovePrivateReadersAsync"/>），反過來會互等成死結；協作者的刪除也才不會與對方鎖成環。
    /// </summary>
    public async Task LockOwnersAsync(IReadOnlyCollection<Guid> files, CancellationToken ct)
    {
        var owners = await db.Attachments.Where(x => files.Contains(x.Id)).Select(x => x.OwnerId).Distinct().Order().ToListAsync(ct);
        foreach (var owner in owners) await quota.LockOwnerAsync(owner, ct);
    }

    /// <summary>擁有者的獨立文件（讀取器），包含已刪除的。</summary>
    public IQueryable<Guid> PrivateReaders(Guid owner) => readers.All.Where(x => x.OwnerId == owner).Select(x => x.Id);

    public Task RemovePrivateReadersAsync(Guid owner, IReadOnlyList<Guid> files, CancellationToken ct) => readers.RemoveAsync(owner, files, ct);

    private IQueryable<Attachment> ExpiredDrafts(DateTimeOffset cutoff, DateTimeOffset interrupted)
    {
        // 只被擁有者自己的私人讀取器引用的檔案，仍視為草稿
        return db.Attachments.Where(x => !x.InLibrary &&
            (x.StorageState == AttachmentStates.Ready && x.CreatedAt < cutoff || x.StorageState == AttachmentStates.Pending && x.CreatedAt < interrupted) &&
            !db.MessageAttachments.Any(l => l.AttachmentId == x.Id) &&
            !db.ResourceAttachments.Any(l => l.AttachmentId == x.Id && !readers.All.Any(r => r.Id == l.ResourceId && r.OwnerId == x.OwnerId)));
    }

    public async Task AbortUploadAsync(Attachment upload, CancellationToken ct)
    {
        Guid cleanupId;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await quota.LockOwnerAsync(upload.OwnerId, ct);
            var state = await db.Attachments.Where(x => x.Id == upload.Id).Select(x => x.StorageState).SingleOrDefaultAsync(ct);
            // 啟用可能已經提交，只是確認回應遺失，所以要先看狀態
            if (state == AttachmentStates.Ready) return;
            // 過期的預約可能已被回收，寫入端才恢復。清理時換一個新的 row 身分，避免較早的收集者
            // 在檢查磁碟後、遲到的寫入實際落地前，把這筆 outbox 刪掉。
            await db.Attachments.Where(x => x.Id == upload.Id && x.StorageState != AttachmentStates.Ready).ExecuteDeleteAsync(ct);
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

    public Task DeletePendingAsync(CancellationToken ct, Guid? id = null) => DeletePendingAsync(id is Guid one ? [one] : null, ct);

    private async Task DeletePendingAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct)
    {
        var rows = await db.Attachments.AsNoTracking().Where(x => x.StorageState == AttachmentStates.Deleting && (ids == null || ids.Contains(x.Id)))
            .OrderBy(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.StorageKey }).ToListAsync(ct);
        foreach (var file in rows)
        {
            try
            {
                await storage.DeleteAsync(file.StorageKey, ct);
                await db.Attachments.Where(x => x.Id == file.Id && x.StorageState == AttachmentStates.Deleting).ExecuteDeleteAsync(ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { LogDeletionDeferred(logger, file.Id, ex.GetType().Name); }
        }
    }

    /// <summary>
    /// 最多回收 100 筆過期草稿，回傳筆數。排程任務回收所有人的；上傳時會傳入 <paramref name="owner"/>，
    /// 讓該擁有者的過期草稿不再計入即將檢查的配額，且只有他們的位元組會在這次呼叫中同步刪除。
    /// </summary>
    public async Task<int> ReclaimAsync(CancellationToken ct, Guid? owner = null)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.DraftRetentionDays);
        var interrupted = now.AddHours(-1);
        var candidates = await ExpiredDrafts(cutoff, interrupted).AsNoTracking().Where(x => owner == null || x.OwnerId == owner)
            .OrderBy(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.OwnerId }).ToListAsync(ct);
        if (owner is not null && candidates.Count == 0) return 0;
        var reclaimed = new List<Guid>();
        foreach (var group in candidates.GroupBy(x => x.OwnerId))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(group.Key, ct);
            var ids = group.Select(x => x.Id).ToArray();
            var expired = await ExpiredDrafts(cutoff, interrupted).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
            await RemovePrivateReadersAsync(group.Key, expired, ct);
            await MarkUnusedAsync(expired, ct);
            await transaction.CommitAsync(ct);
            reclaimed.AddRange(expired);
        }
        if (owner is null) await DeletePendingAsync(null, ct);
        else if (reclaimed.Count > 0) await DeletePendingAsync(reclaimed, ct);
        return reclaimed.Count;
    }

    // 對帳只在排程任務做，不在每次上傳時做。寬限期保護剛寫入的檔案；
    // 一般上傳會先在 SQL 提交預約，之後才會出現檔案。回傳刪除的孤兒檔數。
    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var removed = 0;
        await foreach (var key in storage.StaleKeysAsync(clock.GetUtcNow().AddDays(-1), ct))
        {
            keys.Add(key);
            if (keys.Count < 100) continue;
            removed += await RemoveUntrackedAsync(keys, ct);
            keys.Clear();
        }
        if (keys.Count > 0) removed += await RemoveUntrackedAsync(keys, ct);
        return removed;
    }

    private async Task<int> RemoveUntrackedAsync(IReadOnlySet<string> keys, CancellationToken ct)
    {
        var tracked = await db.Attachments.Where(x => keys.Contains(x.StorageKey)).Select(x => x.StorageKey).ToHashSetAsync(ct);
        var removed = 0;
        foreach (var key in keys.Where(x => !tracked.Contains(x)))
        {
            try
            {
                await storage.DeleteAsync(key, ct);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { LogUntrackedDeletionDeferred(logger, key, ex.GetType().Name); }
        }
        return removed;
    }

    [LoggerMessage(EventId = 6001, EventName = "attachment.delete_deferred", Level = LogLevel.Warning, Message = "Attachment {AttachmentId} deletion awaits retry ({ErrorType}).")]
    private static partial void LogDeletionDeferred(ILogger logger, Guid attachmentId, string errorType);

    [LoggerMessage(EventId = 6002, EventName = "attachment.untracked_delete_deferred", Level = LogLevel.Warning, Message = "Untracked attachment {StorageKey} deletion awaits retry ({ErrorType}).")]
    private static partial void LogUntrackedDeletionDeferred(ILogger logger, string storageKey, string errorType);
}
