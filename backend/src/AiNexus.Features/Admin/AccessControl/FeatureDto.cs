using AiNexus.Data.App.AccessControl;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 已授權的功能。名稱是管理員的覆寫或目錄預設名稱，<c>Route</c> 與 <c>Icon</c> 來自 <see cref="FeatureCatalog"/>：
/// 動作與目錄不認得的功能 <c>Route</c> 為空。選單位置不在這裡，見 <see cref="AccessDto.Navigation"/>。
/// </summary>
public sealed record FeatureDto(string Id, string Name, string Route, string Icon)
{
    /// <summary>資料庫的功能列加上目錄的預設值。目錄不認得的列（舊資料）仍是有效授權，名稱退回 id、沒有頁面與圖示。</summary>
    public static FeatureDto From(Feature row)
    {
        var definition = FeatureCatalog.Find(row.Id);
        return new(row.Id, DisplayName(row.Id, row.Name), definition?.Route ?? "", definition?.Icon ?? "");
    }

    /// <summary>顯示名稱：覆寫優先，其次目錄預設名稱，都沒有時用 id。</summary>
    public static string DisplayName(string id, string? overrideName) => overrideName ?? FeatureCatalog.Find(id)?.Name ?? id;

    /// <summary>依 <see cref="FeatureCatalog.All"/> 的宣告順序排列（目錄不認得的列在最後、依 ID）；<c>/me</c>、管理目錄與稽核目錄共用。</summary>
    public static IEnumerable<Feature> InCatalogOrder(IEnumerable<Feature> rows)
        => rows.OrderBy(x => FeatureCatalog.Position(x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal);
}
