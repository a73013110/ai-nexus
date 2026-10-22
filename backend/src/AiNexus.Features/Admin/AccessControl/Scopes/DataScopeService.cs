namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>
/// <see cref="IDataScope"/> 的實作：範圍取自 <see cref="AccessService"/> 已讀的授權（同一請求共用），
/// 單位由 <see cref="IUnitMembership"/>（Organization）在需要時才計算：最多讀一次所屬與一次下層，不隨單位數增加。
/// </summary>
internal sealed class DataScopeService(AccessService access, IUnitMembership units) : IDataScope
{
    private static readonly IReadOnlySet<string> NoScopes = new HashSet<string>();

    public async Task<EffectiveDataScope> ForAsync(Guid userId, string featureId, CancellationToken ct)
    {
        if (FeatureCatalog.Find(featureId) is not { Scopes.Count: > 0 } feature)
            throw new ArgumentException($"'{featureId}' does not declare data scopes in the feature catalog.", nameof(featureId));
        var grants = await access.ForUserAsync(userId, ct);
        // 主管範圍不授予功能本身：沒有授權（含功能、群組停用）就沒有範圍，policy 照樣回 403。
        if (!grants.Features.Any(x => x.Id == featureId)) return EffectiveDataScope.None;
        var granted = grants.Scopes.GetValueOrDefault(featureId) ?? NoScopes;
        if (granted.Contains(DataScopes.All)) return new(granted, EffectiveDataScope.None.UnitIds);

        var tree = granted.Contains(DataScopes.UnitTree);
        var unit = tree || granted.Contains(DataScopes.Unit);
        var manager = feature.ManagerScope;
        if (!unit && manager is null) return granted.Count == 0 ? EffectiveDataScope.None : new(granted, EffectiveDataScope.None.UnitIds);

        var mine = await units.UnitsOfAsync(userId, ct);
        // 主管範圍只算任主管的單位；同時所屬、但不是主管的單位不因此加入。
        IReadOnlyList<Guid> managed = manager is null ? [] : mine.ManagedUnitIds;
        HashSet<Guid> roots = [], flat = [];
        // unit 是 unit-tree 的子集，有 unit-tree 時直接取整棵下層。
        if (unit) (tree ? roots : flat).UnionWith(mine.UnitIds);
        (manager == DataScopes.UnitTree ? roots : flat).UnionWith(managed);
        if (roots.Count > 0) flat.UnionWith(await units.SubtreeAsync(roots, ct));
        var scopes = managed.Count == 0 ? granted : new HashSet<string>(granted, StringComparer.Ordinal) { manager! };
        return scopes.Count == 0 ? EffectiveDataScope.None : new(scopes, flat);
    }
}
