using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>刪除選單節點。分群必須先清空，不級聯刪除子節點，避免一次誤刪整段選單。會稽核。</summary>
internal sealed class DeleteNavigationItem(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/navigation-items/{id}", async (string id, IRequestActor actor, DeleteNavigationItem handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, ct)).ToHttpResult())
        .WithName("DeleteNavigationItem");

    public Task<Result> HandleAsync(Guid actor, string id, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.AccessControlNavigation, "admin.navigation_item_delete", null, id, token => NavigationRules.SnapshotAsync(db, id, token), async () =>
    {
        var item = await db.Set<NavigationItem>().FindAsync([id], ct);
        if (item is null) return AccessControlErrors.NavigationItemNotFound;
        if (await db.Set<NavigationItem>().AnyAsync(x => x.ParentId == id, ct)) return AccessControlErrors.NavigationItemNotEmpty;
        // 外鍵是 NO ACTION：可見群組要先刪。
        await NavigationRules.ReplaceAudiencesAsync(db, id, [], ct);
        db.Remove(item);
        return Result.Success;
    }, ct);
}
