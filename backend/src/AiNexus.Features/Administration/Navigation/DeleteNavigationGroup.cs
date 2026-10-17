using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Navigation;

/// <summary>刪除選單群組。仍有子群組或功能掛在其下時拒絕，不會級聯刪除。會稽核。</summary>
internal sealed class DeleteNavigationGroup(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/navigation-groups/{id}", async (string id, DeleteNavigationGroup handler, CancellationToken ct) => (await handler.HandleAsync(id, ct)).ToHttpResult())
        .WithName("DeleteNavigationGroup");

    public Task<Result> HandleAsync(string id, CancellationToken ct) => audit.MutateAsync("admin.navigation_group_delete", null, id, async () =>
    {
        var group = await db.Set<NavigationGroup>().FindAsync([id], ct);
        if (group is null) return AdministrationErrors.NavigationGroupNotFound;
        if (await db.Set<NavigationGroup>().AnyAsync(x => x.ParentId == id, ct) || await db.Set<Feature>().AnyAsync(x => x.GroupId == id, ct))
            return AdministrationErrors.NavigationGroupNotEmpty;
        db.Remove(group);
        return Result.Success;
    }, ct);
}
