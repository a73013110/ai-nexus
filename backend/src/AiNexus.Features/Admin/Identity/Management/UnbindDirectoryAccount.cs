using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

/// <summary>
/// 解除使用者與 AD SID 的綁定（ADR 0019：AD 重建同名帳號、SID 已變時用）。SID 改回 <c>managed:</c>，回到「預建」狀態：
/// <c>AdAccount</c> 與登入方式保留，下次以該帳號名 AD 驗證或匯入時綁定新的 SID；要換成不同帳號名用 <see cref="RebindDirectoryAccount"/>。
/// UserId、角色與歷史資料都不動；遞增 <c>SecurityVersion</c>，以舊 SID 建立的工作階段一併失效。
/// </summary>
internal sealed class UnbindDirectoryAccount(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/users/{id:guid}/ad-binding", (Guid id, int profileVersion, ICurrentUser user, UnbindDirectoryAccount handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, profileVersion, ct))
        .Audited(IdentityAudit.DirectoryUnbound)
        .WithName("UnbindUserDirectoryAccount");

    public async Task<Result> HandleAsync(Guid actor, Guid id, int profileVersion, CancellationToken ct)
    {
        // 換掉自己的 SID 會讓自己目前的工作階段失效，交由另一位管理員處理。
        if (id == actor) return AccessControlErrors.Lockout;

        var begun = await write.BeginAsync(AuditTarget.Of(id, token => RebindDirectoryAccount.SnapshotAsync(db, id, token)).Locking(AccessControlLocks.Authorization), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;
        if (!UserAccounts.DirectoryBound(user)) return IdentityErrors.AdNotBound;
        db.ExpectProfileVersion(user, profileVersion);
        user.Sid = UserAccounts.ManagedSid(id);
        user.SecurityVersion++;

        return await transaction.CommitAsync(ct);
    }
}
