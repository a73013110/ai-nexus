using AiNexus.Data.App;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Files.Management;

/// <summary>把呼叫者的一個附件保留到檔案庫；對已在檔案庫的檔案重複保留不會有任何變更。</summary>
internal sealed class RetainLibraryFile(AppDbContext db, AttachmentService files, AttachmentQuota quota) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retain", (Guid id, ICurrentUser user, RetainLibraryFile handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(FilesAudit.FileRetained)
        .WithName("RetainLibraryFile");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await quota.LockOwnerAsync(actor, ct);
        var file = await files.OwnedAsync(actor, id, ct);
        if (!file.IsSuccess) return file.Error;
        if (file.Value.InLibrary) return Result.Success;
        await db.Attachments.Where(x => x.Id == id && x.OwnerId == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
