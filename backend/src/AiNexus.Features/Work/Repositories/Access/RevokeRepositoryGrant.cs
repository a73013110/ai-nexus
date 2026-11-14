using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>
/// 結束一筆有效授權：負責人撤銷人員的讀取、寫入，管理員撤銷任何授權，本人可以放棄自己的。協作者由同步任務移除。會稽核。
/// 其他人一律回找不到，不透露授權是否存在。
/// </summary>
internal sealed class RevokeRepositoryGrant(AppDbContext db, RepositoryAccess access, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/grants/{grantId:guid}", (Guid id, Guid grantId, ICurrentUser user, RevokeRepositoryGrant handler, CancellationToken ct) =>
            handler.HandleAsync(id, grantId, user.Id, ct))
        .Audited(RepositoriesAudit.GrantRevoked)
        .WithName("RevokeRepositoryGrant");

    public async Task<Result> HandleAsync(Guid id, Guid grantId, Guid actor, CancellationToken ct)
    {
        var grant = await db.RepositoryGrants.SingleOrDefaultAsync(x => x.Id == grantId && x.RepositoryId == id && x.EndedAt == null, ct);
        if (grant is null) return RepositoriesErrors.GrantNotFound;
        var manager = await access.ManagerAsync(actor, id, ct);
        var own = grant.UserId == actor;
        var allowed = own || manager == RepositoryManager.Administrator
            || (manager == RepositoryManager.Steward && grant.UserId is not null && grant.Permission != RepositoryPermissions.Admin);
        if (!allowed) return manager == RepositoryManager.None ? RepositoriesErrors.GrantNotFound : RepositoriesErrors.AccessForbidden;

        var reason = own ? RepositoryGrantEndReasons.Released : RepositoryGrantEndReasons.Revoked;
        await access.EndAsync(grant, reason, actor, ct);
        // 同步任務剛好在寫這筆的同步狀態：請使用者重新整理後再撤銷，不覆蓋它的結果。
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return RepositoriesErrors.AccessChanged; }
        audit.Describe(id, grant.UserId, new { account = grant.Login, permission = grant.Permission, status = reason });
        return Result.Success;
    }
}
