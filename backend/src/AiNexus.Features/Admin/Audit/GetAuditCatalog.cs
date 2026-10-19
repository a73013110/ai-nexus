using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Audit;

public sealed record AuditCatalogDto(IReadOnlyList<FeatureDto> Features, IReadOnlyList<ModelDto> Models,
    IReadOnlyList<AuditCategoryDto> Categories, IReadOnlyList<AuditActionDto> Actions);

/// <summary>
/// 稽核頁顯示用的功能（依目錄宣告順序，前端再依區 › 模組分組）與模型名稱，以及分類與動作篩選。
/// 唯讀，且不依賴帳號或政策管理功能。
/// </summary>
internal sealed class GetAuditCatalog(AppDbContext db, ModelCatalog catalog)
{
    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("/catalog", async (GetAuditCatalog handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetAuditCatalog");

    public async Task<AuditCatalogDto> HandleAsync(CancellationToken ct) => new(
        [.. FeatureDto.InCatalogOrder(await db.Set<Feature>().AsNoTracking().ToListAsync(ct)).Select(FeatureDto.From)],
        (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray(),
        AuditCategories.All,
        AuditActions.All);
}
