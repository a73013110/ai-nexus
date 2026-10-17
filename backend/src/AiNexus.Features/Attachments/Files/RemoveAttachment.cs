using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments.Files;

/// <summary>
/// 移除尚未送出的輸入框草稿，或從檔案庫刪除檔案。仍被對話或其他工作區資源持有的檔案會保留；
/// 擁有者對該檔案的私人讀取器會一併移除。位元組在交易提交後才刪除。
/// </summary>
internal sealed class RemoveAttachment(NexusDbContext db, AttachmentService files, AttachmentWriteLock writes, AttachmentQuota quota, AttachmentLifecycle lifecycle)
{
    public static void MapDraft(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, RemoveAttachment handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, fromLibrary: false, ct)).ToHttpResult())
        .WithName("RemoveDraftAttachment");

    public static void MapLibrary(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, RemoveAttachment handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, fromLibrary: true, ct)).ToHttpResult())
        .WithName("DeleteLibraryFile");

    public async Task<Result> HandleAsync(Guid owner, Guid id, bool fromLibrary, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(owner, ct);
            var file = await files.OwnedAsync(owner, id, ct);
            if (!file.IsSuccess) return file.Error;
            // 從輸入框移除重複使用的檔案，絕不能刪掉檔案庫中的原檔。
            if (file.Value.InLibrary && !fromLibrary) return Result.Success;

            // 已送出的檔案由對話保留；其他資源必須自行釋放對檔案的引用。
            if (await db.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct)) return AttachmentsErrors.InUse;
            if (await db.Set<AttachmentReference>().AnyAsync(x => x.AttachmentId == id && !lifecycle.PrivateReaders(owner).Contains(x.ResourceId), ct))
            {
                return AttachmentsErrors.InUse;
            }

            await lifecycle.RemovePrivateReadersAsync(owner, [id], ct);
            await db.Set<Attachment>()
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, false).SetProperty(x => x.StorageState, AttachmentStates.Deleting), ct);
            if (fromLibrary) { db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = id, Action = "file.deleted", Result = "deleted" }); await db.SaveChangesAsync(ct); }
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct, id);
        return Result.Success;
    }
}
