namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>
/// <see cref="IDataScope"/> 的實作：範圍取自 <see cref="AccessService"/> 已讀的授權（同一請求共用），
/// 單位由 <see cref="IUnitMembership"/>（Organization）在需要時才計算。
/// </summary>
internal sealed class DataScopeService(AccessService access, IUnitMembership units) : IDataScope
{
    public async Task<EffectiveDataScope> ForAsync(Guid userId, string featureId, CancellationToken ct)
    {
        if (FeatureCatalog.Find(featureId) is not { Scopes.Count: > 0 })
            throw new ArgumentException($"'{featureId}' does not declare data scopes in the feature catalog.", nameof(featureId));
        if (!(await access.ForUserAsync(userId, ct)).Scopes.TryGetValue(featureId, out var scopes) || scopes.Count == 0) return EffectiveDataScope.None;
        var tree = scopes.Contains(DataScopes.UnitTree);
        if (scopes.Contains(DataScopes.All) || !tree && !scopes.Contains(DataScopes.Unit)) return new(scopes, EffectiveDataScope.None.UnitIds);
        var mine = await units.UnitsOfAsync(userId, ct);
        // unit 是 unit-tree 的子集，有 unit-tree 時直接取整棵下層。
        return new(scopes, tree ? await units.SubtreeAsync(mine, ct) : mine.ToHashSet());
    }
}
