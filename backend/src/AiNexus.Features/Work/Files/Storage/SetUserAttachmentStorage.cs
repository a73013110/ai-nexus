using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Files.Storage;

/// <summary>設定或清除使用者個人的附件容量上限，於擁有者的配額鎖內更新。會稽核。</summary>
internal sealed class SetUserAttachmentStorage(AppDbContext db, AdministrativeAudit audit, AttachmentQuota quota)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/storage", async (Guid id, AttachmentStorageLimitRequest body, ICurrentUser user, SetUserAttachmentStorage handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("SetUserAttachmentStorage");

    public Task<Result> HandleAsync(Guid actor, Guid id, AttachmentStorageLimitRequest request, CancellationToken ct) => audit.MutateAsync(actor, "admin.user_storage", id, id.ToString(), token => SnapshotAsync(id, token), async () =>
    {
        if (request.LimitBytes is < 0 or > FilesOptions.MaximumLimitBytes) return FilesErrors.InvalidStorageLimit;
        if (await quota.LockOwnerAsync(id, ct) != 1) return FilesErrors.AdminResourceNotFound;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (user is null) return FilesErrors.AdminResourceNotFound;
        user.AttachmentLimitBytes = request.LimitBytes;
        return Result.Success;
    }, ct);

    private async Task<object?> SnapshotAsync(Guid id, CancellationToken ct)
        => await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.AttachmentLimitBytes }).SingleOrDefaultAsync(ct);
}
