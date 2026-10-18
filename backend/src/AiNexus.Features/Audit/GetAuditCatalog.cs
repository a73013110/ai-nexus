using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.Models;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Audit;

public sealed record AuditCatalogDto(IReadOnlyList<FeatureDto> Features, IReadOnlyList<ModelDto> Models, IReadOnlyList<NavigationGroupDto> NavigationGroups,
    IReadOnlyList<AuditCategoryDto> Categories, IReadOnlyList<AuditActionDto> Actions);

/// <summary>
/// 稽核頁顯示用的功能、模型與選單群組名稱，以及分類與動作篩選。所有群組都列出，因為已記錄的功能異動是依選單分群排列的。
/// 唯讀，且不依賴帳號或政策管理功能。
/// </summary>
internal sealed class GetAuditCatalog(AppDbContext db, ModelCatalog catalog, AccessService access)
{
    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("/catalog", async (GetAuditCatalog handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetAuditCatalog");

    public async Task<AuditCatalogDto> HandleAsync(CancellationToken ct) => new(
        await db.Set<Feature>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new FeatureDto(x.Id, x.Name, x.Route, x.GroupId, x.Icon, x.SortOrder)).ToArrayAsync(ct),
        (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray(),
        await access.NavigationGroupsAsync(ct),
        AuditCategories.All,
        AuditActions.All);
}
