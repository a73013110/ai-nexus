using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Files.Management;

/// <summary>把呼叫者的一個附件保留到檔案庫；對已在檔案庫的檔案重複保留不會有任何變更。</summary>
internal sealed class RetainLibraryFile(AppDbContext db, AttachmentService files, AttachmentWriteLock writes, AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retain", async (Guid id, ICurrentUser user, RetainLibraryFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .Audited(FilesAudit.FileRetained)
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
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success;
        }
        finally { writes.Gate.Release(); }
    }
}
