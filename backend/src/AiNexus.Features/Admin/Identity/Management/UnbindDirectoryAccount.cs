using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

/// <summary>
/// 解除使用者與 AD SID 的綁定（ADR 0019：AD 重建同名帳號、SID 已變時用）。SID 改回 <c>managed:</c>，回到「預建」狀態：
/// <c>AdAccount</c> 與登入方式保留，下次以該帳號名 AD 驗證或匯入時綁定新的 SID；要換成不同帳號名用 <see cref="RebindDirectoryAccount"/>。
/// UserId、角色與歷史資料都不動；遞增 <c>SecurityVersion</c>，以舊 SID 建立的工作階段一併失效。
/// </summary>
internal sealed class UnbindDirectoryAccount(AppDbContext db, AdministrativeAudit audit, IdentityWriteLock writeLock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/users/{id:guid}/ad-binding", (Guid id, ICurrentUser user, UnbindDirectoryAccount handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("UnbindUserDirectoryAccount");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        // 登入綁定 SID 時也取身分鎖；鎖的順序同 DirectoryProvisioning：身分鎖在稽核交易外先取。
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            return await audit.MutateAsync(actor, FeatureCatalog.IdentityUsers, "admin.user_ad_unbind", id, id.ToString(), token => RebindDirectoryAccount.SnapshotAsync(db, id, token), async () =>
            {
                var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
                if (user is null) return IdentityErrors.UserNotFound;
                // 換掉自己的 SID 會讓自己目前的工作階段失效，交由另一位管理員處理。
                if (id == actor) return AccessControlErrors.Lockout;
                if (!UserAccounts.DirectoryBound(user)) return IdentityErrors.AdNotBound;
                user.Sid = UserAccounts.ManagedSid(id);
                user.SecurityVersion++;
                return Result.Success;
            }, ct);
        }
        finally { writeLock.Gate.Release(); }
    }
}
