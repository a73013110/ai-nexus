using AiNexus.Platform.Validation;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Administration.Roles;

namespace AiNexus.Features.Administration.Users;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record UserRolesRequest(IReadOnlyList<string> RoleIds);

/// <summary>Replaces a user's roles. Audited; refused when it would remove the actor's own administrator access.</summary>
internal sealed class SetUserRoles(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/roles", async (Guid id, UserRolesRequest body, SetUserRoles handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SetUserRoles");

    public Task<Result> HandleAsync(Guid id, UserRolesRequest request, CancellationToken ct) => audit.MutateAsync("admin.user_roles", id, id.ToString(), async () =>
    {
        if (AccessRules.Keys(request.RoleIds) is { } invalid) return invalid;
        if (!await db.Users.AnyAsync(x => x.Id == id, ct)) return AdministrationErrors.NotFound;
        if (await AccessRules.ExistingAsync(db.Set<Role>().Select(x => x.Id), request.RoleIds, ct) is { } unknown) return unknown;
        var old = await db.Set<UserRole>().Where(x => x.UserId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !request.RoleIds.Contains(x.RoleId)));
        db.AddRange(request.RoleIds.Where(x => !old.Any(y => y.RoleId == x)).Select(x => new UserRole { UserId = id, RoleId = x }));
        return Result.Success;
    }, ct);
}
