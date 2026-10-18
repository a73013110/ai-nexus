using AiNexus.Data.App;
using AiNexus.Features.Attachments;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Users;

/// <summary>設定或清除使用者個人的附件容量上限，於擁有者的配額鎖內更新。會稽核。</summary>
internal sealed class SetUserAttachmentStorage(AppDbContext db, AdministrativeAudit audit, AttachmentQuota quota)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/storage", async (Guid id, AttachmentStorageLimitRequest body, SetUserAttachmentStorage handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SetUserAttachmentStorage");

    public Task<Result> HandleAsync(Guid id, AttachmentStorageLimitRequest request, CancellationToken ct) => audit.MutateAsync("admin.user_storage", id, id.ToString(), async () =>
    {
        if (request.LimitBytes is < 0 or > AttachmentOptions.MaximumLimitBytes) return AdministrationErrors.InvalidStorageLimit;
        if (await quota.LockOwnerAsync(id, ct) != 1) return AdministrationErrors.NotFound;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (user is null) return AdministrationErrors.NotFound;
        user.AttachmentLimitBytes = request.LimitBytes;
        return Result.Success;
    }, ct);
}
