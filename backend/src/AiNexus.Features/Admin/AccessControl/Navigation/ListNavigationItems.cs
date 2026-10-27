using AiNexus.Platform.Auditing;
namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>選單管理頁的全部節點（平面清單、含外部連結的可見群組），不依登入者修剪；樹狀結構由前端依 <c>ParentId</c> 組回。</summary>
internal sealed class ListNavigationItems(AccessService access)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/navigation-items", async (ListNavigationItems handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .Audited(AccessControlAudit.NavigationRead)
        .WithName("ListNavigationItems");

    public Task<IReadOnlyList<NavigationItemDto>> HandleAsync(CancellationToken ct) => access.NavigationItemsAsync(ct);
}
