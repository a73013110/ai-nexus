using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments;

/// <summary>
/// Removes an unsent composer draft, or deletes a file from the library. Files held by a conversation or by another
/// workspace resource stay; the owner's private readers of the file are removed with it. Bytes are deleted after commit.
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
            // Removing a reused file from the composer must never delete its library original.
            if (file.Value.InLibrary && !fromLibrary) return Result.Success;
            // Sent files are kept by their conversation; other resources must release the file themselves.
            if (await db.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct)) return AttachmentsErrors.InUse;
            if (await db.Set<AttachmentReference>().AnyAsync(x => x.AttachmentId == id && !lifecycle.PrivateReaders(owner).Contains(x.ResourceId), ct)) return AttachmentsErrors.InUse;
            await lifecycle.RemovePrivateReadersAsync(owner, [id], ct);
            await db.Set<Attachment>().Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, false).SetProperty(x => x.StorageState, AttachmentStates.Deleting), ct);
            if (fromLibrary) { db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = id, Action = "file.deleted", Result = "deleted" }); await db.SaveChangesAsync(ct); }
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct, id);
        return Result.Success;
    }
}
