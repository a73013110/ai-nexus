namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>
/// 使用者目前的有效單位，依範圍宣告（<see cref="DataScopeDefinition.Units"/>、<see cref="DataScopeDefinition.Subtree"/>）分四組，都已含下層與否算好：
/// 所屬、所屬及下層、任主管、任主管及下層。主管的單位不只是勾選的單位（首長單位另管轄上層，ADR 0024），所以下層由 Organization 一併計算。
/// </summary>
public sealed record UnitsOfUser(IReadOnlySet<Guid> Units, IReadOnlySet<Guid> UnitTree, IReadOnlySet<Guid> Managed, IReadOnlySet<Guid> ManagedTree)
{
    /// <summary>範圍依據的單位；不看單位的範圍（<c>own</c>、<c>assigned</c>、<c>all</c>）為空。</summary>
    public IReadOnlySet<Guid> Of(DataScopeDefinition scope) => (scope.Units, scope.Subtree) switch
    {
        (DataScopeUnits.Member, false) => Units,
        (DataScopeUnits.Member, true) => UnitTree,
        (DataScopeUnits.Managed, false) => Managed,
        (DataScopeUnits.Managed, true) => ManagedTree,
        _ => new HashSet<Guid>(),
    };
}

/// <summary>
/// 計算依單位的範圍（<c>unit</c>、<c>unit-tree</c>、<c>managed</c>、<c>managed-tree</c>）需要的組織資料。由 Organization 實作（<c>OrganizationDirectory</c>），同 <see cref="IRequestActor"/> 的做法：
/// AccessControl 被所有模組引用，反過來依賴 Organization 會成環（ADR 0017）。只回答「現在」的有效單位，停用的單位連同下層都不算。
/// </summary>
public interface IUnitMembership
{
    /// <summary>
    /// 使用者目前的四組有效單位，一次讀出：同一個功能有多種範圍時不必多查。帳號停用或刪除時都為空。
    /// </summary>
    Task<UnitsOfUser> UnitsOfAsync(Guid userId, CancellationToken ct);
}
