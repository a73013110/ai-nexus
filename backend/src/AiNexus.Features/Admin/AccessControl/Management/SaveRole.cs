using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Management;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record RoleUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> GroupIds);

/// <summary>建立或更新角色與其所屬群組。會稽核；若會移除執行者自己的角色管理權限則拒絕。</summary>
internal sealed class SaveRole(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/roles/{id}", (string id, RoleUpdateRequest body, IRequestActor actor, SaveRole handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, id, body, ct))
        .Audited(AccessControlAudit.RoleUpdated)
        .WithName("SaveRole");

    public Task<Result> HandleAsync(Guid actor, string id, RoleUpdateRequest request, CancellationToken ct) => write.ExecuteAsync(actor, AuditTarget.Of(FeatureCatalog.AccessControlRoles, id, token => SnapshotAsync(id, token)), async () =>
    {
        if ((AccessRules.Key(id) ?? AccessRules.Name(request.Name) ?? AccessRules.Keys(request.GroupIds)) is { } invalid) return invalid;
        if (await AccessRules.ExistingAsync(db.RoleGroups.Select(x => x.Id), request.GroupIds, ct) is { } unknown) return unknown;
        var role = await db.Roles.FindAsync([id], ct);
        if (role is null) { role = new() { Id = id }; db.Add(role); }
        role.Name = request.Name.Trim(); role.Enabled = request.Enabled;
        var old = await db.RoleGroupRoles.Where(x => x.RoleId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !request.GroupIds.Contains(x.GroupId)));
        db.AddRange(request.GroupIds.Where(x => !old.Any(y => y.GroupId == x)).Select(x => new RoleGroupRole { RoleId = id, GroupId = x }));
        return Result.Success;
    }, ct);

    private async Task<object?> SnapshotAsync(string id, CancellationToken ct)
    {
        var value = await db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return value is null ? null : new { value.Name, value.Enabled, groupIds = await db.RoleGroupRoles.AsNoTracking().Where(x => x.RoleId == id).OrderBy(x => x.GroupId).Select(x => x.GroupId).ToArrayAsync(ct) };
    }
}
