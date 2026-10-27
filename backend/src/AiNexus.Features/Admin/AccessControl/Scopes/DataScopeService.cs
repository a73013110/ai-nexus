namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>
/// <see cref="IDataScope"/> 的實作：範圍取自 <see cref="AccessService"/> 已讀的授權（同一請求共用），
/// 單位由 <see cref="IUnitMembership"/>（Organization）在需要時才計算：最多讀一次所屬與一次下層，不隨單位數增加。
/// 每個範圍算哪些單位由 <see cref="DataScopeDefinition"/> 宣告，這裡不分辨成員範圍或主管範圍。
/// </summary>
internal sealed class DataScopeService(AccessService access, IUnitMembership units) : IDataScope
{
    public async Task<EffectiveDataScope> ForAsync(Guid userId, string featureId, CancellationToken ct)
    {
        if (FeatureCatalog.Find(featureId) is not { Scopes.Count: > 0 })
            throw new ArgumentException($"'{featureId}' does not declare data scopes in the feature catalog.", nameof(featureId));
        var grants = await access.ForUserAsync(userId, ct);
        // 範圍不授予功能本身：沒有授權（含功能、群組停用）就沒有範圍，policy 照樣回 403。
        if (!grants.Features.Any(x => x.Id == featureId)) return EffectiveDataScope.None;
        var granted = Granted(grants, featureId);
        var mine = Unbounded(granted) || granted.All(x => x.Units == DataScopeUnits.None) ? null : await units.UnitsOfAsync(userId, ct);
        var scopes = Effective(granted, mine?.ManagedUnitIds.Count > 0);
        if (scopes.Count == 0) return EffectiveDataScope.None;
        if (mine is null) return new(Codes(scopes), EffectiveDataScope.None.UnitIds);

        HashSet<Guid> roots = [], flat = [];
        // unit 是 unit-tree 的子集，兩者都有時所屬單位同時進 roots 與 flat，聯集不變。
        foreach (var scope in scopes.Where(x => x.Units != DataScopeUnits.None)) (scope.Subtree ? roots : flat).UnionWith(mine.Of(scope.Units));
        if (roots.Count > 0) flat.UnionWith(await units.SubtreeAsync(roots, ct));
        return new(Codes(scopes), flat);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ScopesAsync(Guid userId, CancellationToken ct)
    {
        var grants = await access.ForUserAsync(userId, ct);
        var features = grants.Features.Where(x => FeatureCatalog.Find(x.Id) is { Scopes.Count: > 0 }).Select(x => (x.Id, Granted: Granted(grants, x.Id))).ToList();
        // 與 ForAsync 同一條規則。這裡只需知道有沒有任主管的單位，所有功能共用一次讀取，沒有功能會用到時不讀。
        var managing = features.Any(x => !Unbounded(x.Granted) && x.Granted.Any(IsManagerScope)) && (await units.UnitsOfAsync(userId, ct)).ManagedUnitIds.Count > 0;
        Dictionary<string, IReadOnlyList<string>> result = new(StringComparer.Ordinal);
        foreach (var (id, granted) in features)
            if (Effective(granted, managing) is { Count: > 0 } scopes) result[id] = [.. scopes.Select(x => x.Id)];
        return result;
    }

    /// <summary>授權的範圍，依 <see cref="DataScopes.Definitions"/> 由窄到寬（同時含成員範圍與主管範圍）。</summary>
    private static List<DataScopeDefinition> Granted(AccessDto grants, string featureId)
        => [.. DataScopes.InOrder(grants.GrantedScopes.GetValueOrDefault(featureId) ?? new HashSet<string>())];

    private static bool Unbounded(IEnumerable<DataScopeDefinition> granted) => granted.Any(x => x.Id == DataScopes.All);

    private static bool IsManagerScope(DataScopeDefinition scope) => scope.Units == DataScopeUnits.Managed;

    /// <summary>
    /// 主管範圍只對實際任主管的人生效，結果才不會讓非主管看起來「看得到別人」；已有 <c>all</c> 時不另外計算，也不列主管範圍。
    /// </summary>
    private static List<DataScopeDefinition> Effective(List<DataScopeDefinition> granted, bool managing)
        => [.. granted.Where(x => !IsManagerScope(x) || (managing && !Unbounded(granted)))];

    private static HashSet<string> Codes(IEnumerable<DataScopeDefinition> scopes) => scopes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
}
