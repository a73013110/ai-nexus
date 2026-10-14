namespace AiNexus.Features.AccessControl.Navigation;

/// <summary>Rules over the menu-group hierarchy, shared by the menu payload and the administrator's group edits.</summary>
public static class NavigationTree
{
    /// <summary>
    /// The groups that hold at least one of <paramref name="features"/> with a page, directly or in a subgroup, with every
    /// ancestor of such a group. Grant-only features (empty route) never keep a group visible.
    /// </summary>
    public static IReadOnlyList<NavigationGroupDto> VisibleGroups(IReadOnlyList<NavigationGroupDto> groups, IEnumerable<FeatureDto> features)
    {
        var parents = groups.ToDictionary(x => x.Id, x => x.ParentId, StringComparer.Ordinal);
        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            if (string.IsNullOrEmpty(feature.Route)) continue;
            // The visited set stops at a group already shown, and guards against a cycle in hand-edited data.
            for (var id = feature.GroupId; id is not null && parents.ContainsKey(id) && visible.Add(id);) id = parents[id];
        }
        return groups.Where(x => visible.Contains(x.Id)).ToArray();
    }

    /// <summary>Whether placing <paramref name="id"/> under <paramref name="parentId"/> would make it its own ancestor.</summary>
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
