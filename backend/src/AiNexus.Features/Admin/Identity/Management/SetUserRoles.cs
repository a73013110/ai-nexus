using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

[ValidatedInHandler("Format failures return the specific codes of AccessRules, checked in the handler before the transaction.")]
public sealed record UserRolesRequest(IReadOnlyList<string> RoleIds, int ProfileVersion);

/// <summary>整批取代使用者的角色。會稽核；若會移除執行者自己的管理員權限則拒絕。</summary>
internal sealed class SetUserRoles(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/roles", (Guid id, UserRolesRequest body, ICurrentUser user, SetUserRoles handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct))
        .Audited(IdentityAudit.UserRoleUpdated)
        .WithName("SetUserRoles");

    public async Task<Result> HandleAsync(Guid actor, Guid id, UserRolesRequest request, CancellationToken ct)
    {
        if (AccessRules.Keys(request.RoleIds) is { } invalid) return invalid;

        var begun = await write.BeginAsync(AuditTarget.Of(id, token => SnapshotAsync(id, token)).KeepingAccess(actor, FeatureCatalog.IdentityUsers), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return IdentityErrors.AdminResourceNotFound;
        if (await AccessRules.ExistingAsync(db.Roles.Select(x => x.Id), request.RoleIds, ct) is { } unknown) return unknown;
        // 角色在子表，主列的版本也要比對並升版，否則兩人同時改角色時後者會蓋掉前者。
        db.ExpectProfileVersion(user, request.ProfileVersion);
        await ReplaceAsync(db, id, request.RoleIds, ct);

        return await transaction.CommitAsync(ct);
    }

    private async Task<object?> SnapshotAsync(Guid id, CancellationToken ct) => new { roleIds = await RoleIdsAsync(db, id, ct) };

    /// <summary>把使用者的角色改成 <paramref name="roleIds"/>：只刪除與新增差異，未變的指派保持原列。呼叫端負責先驗證角色並儲存。</summary>
    internal static async Task ReplaceAsync(AppDbContext db, Guid userId, IReadOnlyList<string> roleIds, CancellationToken ct)
    {
        var old = await db.UserRoles.Where(x => x.UserId == userId).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !roleIds.Contains(x.RoleId)));
        db.AddRange(roleIds.Where(x => !old.Any(y => y.RoleId == x)).Select(x => new UserRole { UserId = userId, RoleId = x }));
    }

    /// <summary>稽核快照用的角色清單，排序固定，前後快照才能逐項比對。</summary>
    internal static Task<string[]> RoleIdsAsync(AppDbContext db, Guid userId, CancellationToken ct)
        => db.UserRoles.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(ct);
}
