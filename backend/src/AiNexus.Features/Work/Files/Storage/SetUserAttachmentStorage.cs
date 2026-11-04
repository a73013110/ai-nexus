using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Files.Storage;

/// <summary>設定或清除使用者個人的附件容量上限，於擁有者的配額鎖內更新。會稽核。</summary>
internal sealed class SetUserAttachmentStorage(AppDbContext db, AdministrativeWrite write, AttachmentQuota quota) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/storage", (Guid id, AttachmentStorageLimitRequest body, SetUserAttachmentStorage handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .Audited(FilesAudit.StorageLimitUpdated)
        .WithName("SetUserAttachmentStorage");

    public async Task<Result> HandleAsync(Guid id, AttachmentStorageLimitRequest request, CancellationToken ct)
    {
        if (request.LimitBytes is < 0 or > FilesOptions.MaximumLimitBytes) return FilesErrors.InvalidStorageLimit;

        var begun = await write.BeginAsync(AuditTarget.Of(id, async token =>
            await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.AttachmentLimitBytes }).SingleOrDefaultAsync(token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        if (await quota.LockOwnerAsync(id, ct) != 1) return FilesErrors.AdminResourceNotFound;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (user is null) return FilesErrors.AdminResourceNotFound;
        user.AttachmentLimitBytes = request.LimitBytes;

        return await transaction.CommitAsync(ct);
    }
}
