using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Audit;

public sealed record AuditCatalogDto(IReadOnlyList<FeatureDto> Features, IReadOnlyList<ModelDto> Models,
    IReadOnlyList<AuditCategoryDto> Categories, IReadOnlyList<AuditModuleDto> Modules, IReadOnlyList<AuditActionDto> Actions, IReadOnlyList<NavigationSectionDto> NavigationSections);

/// <summary>
/// 稽核頁與功能使用量頁顯示用的功能（依目錄宣告順序）、功能的選單位置（前端據此把功能異動照側欄分組）與模型名稱，
/// 以及分類、模組與動作（<see cref="AuditActions"/>）。
/// 唯讀，且不依賴帳號或政策管理功能。
/// </summary>
internal sealed class GetAuditCatalog(AppDbContext db, ModelCatalog catalog, AccessService access) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder audit) => audit
        .MapGet("/catalog", (GetAuditCatalog handler, CancellationToken ct) =>
            handler.HandleAsync(ct))
        .NotAudited("稽核篩選用的固定目錄")
        .WithName("GetAuditCatalog");

    public async Task<AuditCatalogDto> HandleAsync(CancellationToken ct) => new(
        [.. (await db.Features.AsNoTracking().ToListAsync(ct)).InCatalogOrder().Select(FeatureDto.From)],
        (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray(),
        AuditCategories.All,
        AuditActions.Modules,
        AuditActions.All,
        await access.NavigationSectionsAsync(ct));
}
