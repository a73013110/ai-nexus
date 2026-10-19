using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Management;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record UserRolesRequest(IReadOnlyList<string> RoleIds);

/// <summary>整批取代使用者的角色。會稽核；若會移除執行者自己的管理員權限則拒絕。</summary>
internal sealed class SetUserRoles(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/roles", async (Guid id, UserRolesRequest body, ICurrentUser user, SetUserRoles handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("SetUserRoles");

    public Task<Result> HandleAsync(Guid actor, Guid id, UserRolesRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.IdentityUsers, "admin.user_roles", id, id.ToString(), token => SnapshotAsync(id, token), async () =>
    {
        if (AccessRules.Keys(request.RoleIds) is { } invalid) return invalid;
        if (!await db.Users.AnyAsync(x => x.Id == id, ct)) return IdentityErrors.AdminResourceNotFound;
        if (await AccessRules.ExistingAsync(db.Set<Role>().Select(x => x.Id), request.RoleIds, ct) is { } unknown) return unknown;
        var old = await db.Set<UserRole>().Where(x => x.UserId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !request.RoleIds.Contains(x.RoleId)));
        db.AddRange(request.RoleIds.Where(x => !old.Any(y => y.RoleId == x)).Select(x => new UserRole { UserId = id, RoleId = x }));
        return Result.Success;
    }, ct);

    private async Task<object?> SnapshotAsync(Guid id, CancellationToken ct)
        => new { roleIds = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == id).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(ct) };
}
