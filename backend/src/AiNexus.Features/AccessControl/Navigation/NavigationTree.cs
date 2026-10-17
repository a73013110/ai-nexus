namespace AiNexus.Features.AccessControl.Navigation;

/// <summary>選單分群階層的規則，由選單 payload 與管理者的分群編輯共用。</summary>
public static class NavigationTree
{
    /// <summary>
    /// 含有至少一個有頁面功能（直接或透過子分群）的分群，連同這些分群的所有祖先。純授權功能（Route 為空）不會讓分群顯示。
    /// </summary>
    public static IReadOnlyList<NavigationGroupDto> VisibleGroups(IReadOnlyList<NavigationGroupDto> groups, IEnumerable<FeatureDto> features)
    {
        var parents = groups.ToDictionary(x => x.Id, x => x.ParentId, StringComparer.Ordinal);
        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            if (string.IsNullOrEmpty(feature.Route)) continue;
            // visible 集合同時作為走訪的停止條件：遇到已加入的分群就停；也避免手動改過的資料有循環時無窮迴圈。
            for (var id = feature.GroupId; id is not null && parents.ContainsKey(id) && visible.Add(id);) id = parents[id];
        }
        return groups.Where(x => visible.Contains(x.Id)).ToArray();
    }

    /// <summary>判斷把 <paramref name="id"/> 放到 <paramref name="parentId"/> 之下，是否會讓它變成自己的祖先。</summary>
    public static bool CreatesCycle(IReadOnlyDictionary<string, string?> parents, string id, string? parentId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = parentId; current is not null; current = parents.GetValueOrDefault(current))
        {
            if (current == id || !seen.Add(current)) return true;
        }
        return false;
    }
}
