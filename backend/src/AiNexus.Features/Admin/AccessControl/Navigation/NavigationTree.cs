using AiNexus.Data.App.AccessControl;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>選單樹的純函式：使用者可見的樹（<c>/me</c> 與存取預覽共用），以及管理端移動節點時的成環與深度檢查。</summary>
public static class NavigationTree
{
    /// <summary>深度上限，最上層算第 1 層。側欄與「全部功能」的版面只設計到這一層。</summary>
    public const int MaxDepth = 4;

    /// <summary>
    /// 依使用者的授權修剪並攤平選單：
    /// page 要功能已授權且啟用（<paramref name="features"/> 已是這個集合）並且是目錄的頁面；
    /// link 要 audiences 為空或使用者屬於其中任一有效群組（<paramref name="groups"/>），且主機仍在允許清單；
    /// group 沒有可見子節點就不輸出，只剩一個時以該子節點取代自己（遞迴套用，單一路徑會一路攤到葉節點）。
    /// 同層依 <c>SortOrder</c>、再依 Id（ordinal）排序。
    /// </summary>
    public static IReadOnlyList<NavigationNodeDto> Build(IReadOnlyList<NavigationItemDto> items, IEnumerable<FeatureDto> features,
        IEnumerable<string> groups, IReadOnlyList<string> linkHosts)
    {
        var granted = features.GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var memberOf = groups.ToHashSet(StringComparer.Ordinal);
        var children = items.Where(x => x.ParentId is not null).ToLookup(x => x.ParentId!, StringComparer.Ordinal);
        // 走過的節點不再進入：手動改過的資料即使成環也不會無窮遞迴。
        var visited = new HashSet<string>(StringComparer.Ordinal);
        return Visible(items.Where(x => x.ParentId is null));

        IReadOnlyList<NavigationNodeDto> Visible(IEnumerable<NavigationItemDto> level) =>
            [.. level.OrderBy(x => x.SortOrder).ThenBy(x => x.Id, StringComparer.Ordinal).Select(Node).OfType<NavigationNodeDto>()];

        NavigationNodeDto? Node(NavigationItemDto item)
        {
            if (!visited.Add(item.Id)) return null;
            switch (item.Kind)
            {
                case NavigationItem.PageKind:
                    if (item.FeatureId is null || !granted.TryGetValue(item.FeatureId, out var feature)
                        || FeatureCatalog.Find(item.FeatureId) is not { Kind: FeatureKind.Page } page) return null;
                    return new(item.Id, item.Kind, feature.Name, page.Icon, page.Id, page.Route, null, false, []);
                case NavigationItem.LinkKind:
                    if (item.AudienceGroupIds.Count > 0 && !item.AudienceGroupIds.Any(memberOf.Contains)) return null;
                    if (!NavigationLinks.HostAllowed(item.Url, linkHosts)) return null;
                    return new(item.Id, item.Kind, item.Name ?? "", item.Icon, null, null, item.Url, item.OpenInNewTab, []);
                case NavigationItem.GroupKind:
                    var visible = Visible(children[item.Id]);
                    return visible.Count switch
                    {
                        0 => null,
                        1 => visible[0],
                        _ => new(item.Id, item.Kind, item.Name ?? "", item.Icon, null, null, null, false, visible),
                    };
                default:
                    return null;
            }
        }
    }

    /// <summary>父子關係中是否有任何節點是自己的祖先（含以自己為父）。指向不存在節點的父層視為最上層。</summary>
    public static bool HasCycle(IReadOnlyDictionary<string, string?> parents)
    {
        var safe = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in parents.Keys)
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            for (var current = start; !safe.Contains(current); current = parents[current]!)
            {
                if (!path.Add(current)) return true;
                if (parents[current] is not { } parent || !parents.ContainsKey(parent)) break;
            }
            safe.UnionWith(path);
        }
        return false;
    }

    /// <summary>
    /// 整棵樹最深的層數（最上層為 1）。管理端把變更套到完整的父子關係後再算一次，移動整個子樹時子孫的深度也一起算到。
    /// 成環會無窮遞迴，呼叫端必須先以 <see cref="HasCycle"/> 擋下。
    /// </summary>
    public static int Deepest(IReadOnlyDictionary<string, string?> parents)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        return parents.Keys.Select(Depth).DefaultIfEmpty(0).Max();

        int Depth(string id)
        {
            if (depths.TryGetValue(id, out var known)) return known;
            var parent = parents[id];
            var depth = parent is null || !parents.ContainsKey(parent) ? 1 : Depth(parent) + 1;
            depths[id] = depth;
            return depth;
        }
    }
}
