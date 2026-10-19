using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 選單節點需要查資料或設定的規則，在 <see cref="AdministrativeAudit"/> 的交易內執行，被拒絕的變更仍會留下錯誤代碼的稽核。
/// 結構規則一律套在「變更後的完整節點清單」上檢查，新增、修改與批次移動共用同一套判斷。
/// </summary>
internal static class NavigationRules
{
    public const int MaxSortOrder = 10000, SortStep = 10;

    /// <summary>
    /// 父節點必須存在且是分群、不可成環、深度不超過 <see cref="NavigationTree.MaxDepth"/>、同一層不可重複同一頁面。
    /// 最後一條資料庫的唯一索引也會擋，但在這裡先擋才能回固定的錯誤代碼並寫稽核。
    /// </summary>
    public static Error? Structure(IReadOnlyCollection<NavigationItem> items)
    {
        var byId = items.ToDictionary(x => x.Id, StringComparer.Ordinal);
        if (items.Any(x => x.ParentId is { } parent && (!byId.TryGetValue(parent, out var group) || group.Kind != NavigationItem.GroupKind)))
            return AccessControlErrors.NavigationParentInvalid;
        var parents = items.ToDictionary(x => x.Id, x => x.ParentId, StringComparer.Ordinal);
        if (NavigationTree.HasCycle(parents)) return AccessControlErrors.NavigationItemCycle;
        if (NavigationTree.Deepest(parents) > NavigationTree.MaxDepth) return AccessControlErrors.NavigationDepthExceeded;
        return items.Where(x => x.FeatureId is not null).GroupBy(x => (x.ParentId, x.FeatureId)).Any(x => x.Count() > 1)
            ? AccessControlErrors.NavigationPageDuplicate
            : null;
    }

    /// <summary>新增與修改共用的內容檢查：page 看功能（<see cref="PageAsync"/>），link 看主機與可見群組（<see cref="LinkAsync"/>）。</summary>
    public static async Task<Error?> ContentAsync(AppDbContext db, INavigationItemContent content, IReadOnlyList<string> hosts, CancellationToken ct) => content.Kind switch
    {
        NavigationItem.PageKind => await PageAsync(db, content.FeatureId!, ct),
        NavigationItem.LinkKind => await LinkAsync(db, content.Url!, content.AudienceGroupIds ?? [], hosts, ct),
        _ => null,
    };

    /// <summary>把內容欄位寫進節點（新增與修改共用）。<c>OpenInNewTab</c> 只對 link 有意義，其他種類一律存成預設值。</summary>
    public static void Apply(NavigationItem item, INavigationItemContent content)
    {
        item.FeatureId = content.FeatureId; item.Name = content.Name?.Trim(); item.Url = content.Url; item.Icon = content.Icon;
        item.OpenInNewTab = content.Kind != NavigationItem.LinkKind || content.OpenInNewTab;
    }

    /// <summary>page 的功能必須是目錄的頁面且已寫進 <c>Features</c>；動作與閱讀器沒有網址，放進選單只會是死連結。</summary>
    private static async Task<Error?> PageAsync(AppDbContext db, string featureId, CancellationToken ct)
        => FeatureCatalog.Find(featureId) is { Kind: FeatureKind.Page } && await db.Set<Feature>().AnyAsync(x => x.Id == featureId, ct)
            ? null
            : AccessControlErrors.UnknownAccessId;

    /// <summary>外部連結的主機必須在允許清單內，可見群組必須存在（停用的群組可以指定，停用期間只是沒有人因它而看見）。</summary>
    private static async Task<Error?> LinkAsync(AppDbContext db, string url, IReadOnlyList<string> audiences, IReadOnlyList<string> hosts, CancellationToken ct)
    {
        if (!NavigationLinks.HostAllowed(url, hosts)) return AccessControlErrors.NavigationLinkHostNotAllowed;
        return await AccessRules.ExistingAsync(db.Set<RoleGroup>().Select(x => x.Id), audiences, ct);
    }

    /// <summary>以 <paramref name="groups"/> 取代節點的可見群組；外鍵是 NO ACTION，刪除節點前也要先呼叫這裡清空。</summary>
    public static async Task ReplaceAudiencesAsync(AppDbContext db, string id, IReadOnlyList<string> groups, CancellationToken ct)
    {
        var old = await db.Set<NavigationItemAudience>().Where(x => x.NavigationItemId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !groups.Contains(x.GroupId)));
        db.AddRange(groups.Where(x => !old.Any(y => y.GroupId == x)).Select(x => new NavigationItemAudience { NavigationItemId = id, GroupId = x }));
    }

    /// <summary>
    /// 新增、修改與刪除單一節點共用的稽核快照（<c>admin.navigation_item*</c>）。不放網址：稽核遮罩會把任何 URL 換成固定字樣，存了也看不到。
    /// </summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, string id, CancellationToken ct)
    {
        var item = await db.Set<NavigationItem>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        var audiences = await db.Set<NavigationItemAudience>().AsNoTracking().Where(x => x.NavigationItemId == id).OrderBy(x => x.GroupId).Select(x => x.GroupId).ToArrayAsync(ct);
        return new { item.ParentId, item.Kind, item.FeatureId, item.Name, item.OpenInNewTab, item.Icon, item.SortOrder, audienceGroupIds = audiences };
    }
}
