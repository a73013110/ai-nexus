using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>刪除選單群組。仍有子群組或功能掛在其下時拒絕，不會級聯刪除。會稽核。</summary>
internal sealed class DeleteNavigationGroup(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/navigation-groups/{id}", async (string id, IRequestActor actor, DeleteNavigationGroup handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, ct)).ToHttpResult())
        .WithName("DeleteNavigationGroup");

    public Task<Result> HandleAsync(Guid actor, string id, CancellationToken ct) => audit.MutateAsync(actor, "admin.navigation_group_delete", null, id, token => NavigationRules.SnapshotAsync(db, id, token), async () =>
    {
        var group = await db.Set<NavigationGroup>().FindAsync([id], ct);
        if (group is null) return AccessControlErrors.NavigationGroupNotFound;
        if (await db.Set<NavigationGroup>().AnyAsync(x => x.ParentId == id, ct) || await db.Set<Feature>().AnyAsync(x => x.GroupId == id, ct))
            return AccessControlErrors.NavigationGroupNotEmpty;
        db.Remove(group);
        return Result.Success;
    }, ct);
}
