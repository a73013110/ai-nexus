using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments;

/// <summary>Keeps one of the caller's attachments in the file library. Retaining a library file again changes nothing.</summary>
internal sealed class RetainLibraryFile(NexusDbContext db, AttachmentService files, AttachmentWriteLock writes, AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retain", async (Guid id, ICurrentUser user, RetainLibraryFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("RetainLibraryFile");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(actor, ct);
            var file = await files.OwnedAsync(actor, id, ct);
            if (!file.IsSuccess) return file.Error;
            if (file.Value.InLibrary) return Result.Success;
            await db.Set<Attachment>().Where(x => x.Id == id && x.OwnerId == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "file.retained", Result = "saved" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success;
        }
        finally { writes.Gate.Release(); }
    }
}
