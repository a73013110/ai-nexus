using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Navigation;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (NavigationRules).")]
public sealed record NavigationGroupUpdateRequest(string Name, string? ParentId, int SortOrder);

/// <summary>Renames, moves or reorders a menu group. Audited; refused when the new parent is the group itself or one of its subgroups.</summary>
internal sealed class UpdateNavigationGroup(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/navigation-groups/{id}", async (string id, NavigationGroupUpdateRequest body, UpdateNavigationGroup handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("UpdateNavigationGroup");

    public Task<Result> HandleAsync(string id, NavigationGroupUpdateRequest request, CancellationToken ct) => audit.MutateAsync("admin.navigation_group", null, id, async () =>
    {
        if ((AccessRules.Name(request.Name) ?? NavigationRules.Order(request.SortOrder)) is { } invalid) return invalid;
        var group = await db.Set<NavigationGroup>().FindAsync([id], ct);
        if (group is null) return AdministrationErrors.NavigationGroupNotFound;
        if (await NavigationRules.ExistingGroupAsync(db, request.ParentId, ct) is { } missing) return missing;
        var parents = await db.Set<NavigationGroup>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, StringComparer.Ordinal, ct);
        if (NavigationTree.CreatesCycle(parents, id, request.ParentId)) return AdministrationErrors.NavigationGroupCycle;
        group.Name = request.Name.Trim(); group.ParentId = request.ParentId; group.SortOrder = request.SortOrder;
        return Result.Success;
    }, ct);
}
