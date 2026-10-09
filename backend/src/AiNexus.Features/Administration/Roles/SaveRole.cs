using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Roles;

/// <summary>Not a request validator: format failures are audited inside the administrative transaction (see <see cref="AccessRules"/>).</summary>
public sealed record RoleUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> GroupIds);

/// <summary>Creates or updates a role and its groups. Audited; refused when it would remove the actor's own administrator access.</summary>
internal sealed class SaveRole(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/roles/{id}", async (string id, RoleUpdateRequest body, SaveRole handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SaveRole");

    public Task<Result> HandleAsync(string id, RoleUpdateRequest request, CancellationToken ct) => audit.TryMutateAsync("admin.role", null, id, async () =>
    {
        if ((AccessRules.Key(id) ?? AccessRules.Name(request.Name) ?? AccessRules.Keys(request.GroupIds)) is { } invalid) return invalid;
        if (await AccessRules.ExistingAsync(db.Set<RoleGroup>().Select(x => x.Id), request.GroupIds, ct) is { } unknown) return unknown;
        var role = await db.Set<Role>().FindAsync([id], ct);
        if (role is null) { role = new() { Id = id }; db.Add(role); }
        role.Name = request.Name.Trim(); role.Enabled = request.Enabled;
        var old = await db.Set<RoleGroupRole>().Where(x => x.RoleId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !request.GroupIds.Contains(x.GroupId)));
        db.AddRange(request.GroupIds.Where(x => !old.Any(y => y.GroupId == x)).Select(x => new RoleGroupRole { RoleId = id, GroupId = x }));
        return Result.Success;
    }, ct);
}
