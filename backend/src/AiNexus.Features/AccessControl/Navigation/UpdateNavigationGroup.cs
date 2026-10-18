using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.AccessControl.Management;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.AccessControl.Navigation;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (NavigationRules).")]
public sealed record NavigationGroupUpdateRequest(string Name, string? ParentId, int SortOrder);

/// <summary>更新選單群組的名稱、父層與排序。會稽核；新父層若是自己或自己的子孫則拒絕，以防止循環。</summary>
internal sealed class UpdateNavigationGroup(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/navigation-groups/{id}", async (string id, NavigationGroupUpdateRequest body, IRequestActor actor, UpdateNavigationGroup handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, body, ct)).ToHttpResult())
        .WithName("UpdateNavigationGroup");

    public Task<Result> HandleAsync(Guid actor, string id, NavigationGroupUpdateRequest request, CancellationToken ct) => audit.MutateAsync(actor, "admin.navigation_group", null, id, token => NavigationRules.SnapshotAsync(db, id, token), async () =>
    {
        if ((AccessRules.Name(request.Name) ?? NavigationRules.Order(request.SortOrder)) is { } invalid) return invalid;
        var group = await db.Set<NavigationGroup>().FindAsync([id], ct);
        if (group is null) return AccessControlErrors.NavigationGroupNotFound;
        if (await NavigationRules.ExistingGroupAsync(db, request.ParentId, ct) is { } missing) return missing;
        var parents = await db.Set<NavigationGroup>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, StringComparer.Ordinal, ct);
        if (NavigationTree.CreatesCycle(parents, id, request.ParentId)) return AccessControlErrors.NavigationGroupCycle;
        group.Name = request.Name.Trim(); group.ParentId = request.ParentId; group.SortOrder = request.SortOrder;
        return Result.Success;
    }, ct);
}
