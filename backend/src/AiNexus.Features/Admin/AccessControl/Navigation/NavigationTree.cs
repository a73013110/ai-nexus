using AiNexus.Data.App.AccessControl;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>選單樹的純函式：使用者可見的樹（<c>/me</c> 與存取預覽共用）、功能在完整選單上的位置，以及管理端移動節點時的成環與深度檢查。</summary>
public static class NavigationTree
{
    /// <summary>深度上限，最上層算第 1 層。側欄與「全部功能」的版面只設計到這一層。</summary>
    public const int MaxDepth = 4;

    /// <summary>
    /// 依使用者的授權修剪選單：
    /// page 要功能已授權且啟用（<paramref name="features"/> 已是這個集合）並且是目錄的頁面；
    /// link 要 audiences 為空或使用者屬於其中任一有效群組（<paramref name="groups"/>），且主機仍在允許清單；
    /// group 沒有可見子節點就不輸出（逐層判斷，子分群被修剪光的上層也跟著不輸出）；只剩一項仍保留分群，側欄的分群結構不隨授權多寡改變。
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
                    return visible.Count == 0 ? null : new(item.Id, item.Kind, item.Name ?? "", item.Icon, null, null, null, false, visible);
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 完整選單樹（不依使用者修剪、不攤平）上每個功能的位置，依選單順序（深度優先）：
    /// 頁面放在它第一次出現的分群，動作接在上層頁面之後（依目錄順序），區段依第一個功能出現的順序。
    /// 授權清單要分組的是「別人」的授權，所以不能用 <c>/me</c> 那棵依檢視者修剪過的樹。
    /// 沒放進選單的功能不在結果中，由呼叫端另外歸組。
    /// </summary>
    public static IReadOnlyList<NavigationSectionDto> Sections(IReadOnlyList<NavigationItemDto> items)
    {
        var children = items.ToLookup(x => x.ParentId ?? "", StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var sections = new List<(string Id, IReadOnlyList<string> Path, List<string> Features)>();
        Visit("", []);
        return [.. sections.Select(x => new NavigationSectionDto(x.Id, x.Path, x.Features))];

        void Visit(string group, IReadOnlyList<string> path)
        {
            foreach (var item in children[group].OrderBy(x => x.SortOrder).ThenBy(x => x.Id, StringComparer.Ordinal))
            {
                if (!visited.Add(item.Id)) continue;
                if (item.Kind == NavigationItem.GroupKind) Visit(item.Id, [.. path, item.Name ?? ""]);
                else if (item.Kind == NavigationItem.PageKind && item.FeatureId is { } feature
                    && FeatureCatalog.Find(feature) is { Kind: FeatureKind.Page } && placed.Add(feature))
                {
                    var section = sections.FindIndex(x => x.Id == group);
                    if (section < 0)
                    {
                        sections.Add((group, path, []));
                        section = sections.Count - 1;
                    }
                    sections[section].Features.Add(feature);
                    sections[section].Features.AddRange(FeatureCatalog.ActionsOf(feature).Select(x => x.Id).Where(placed.Add));
                }
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
