namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>
/// 計算 <c>unit</c>、<c>unit-tree</c> 範圍需要的組織資料。由 Organization 實作（<c>OrganizationDirectory</c>），同 <see cref="IRequestActor"/> 的做法：
/// AccessControl 被所有模組引用，反過來依賴 Organization 會成環（ADR 0017）。只回答「現在」的有效單位，停用的單位連同下層都不算。
/// </summary>
public interface IUnitMembership
{
    /// <summary>使用者目前所屬的有效單位；帳號停用或刪除時為空。</summary>
    Task<IReadOnlyList<Guid>> UnitsOfAsync(Guid userId, CancellationToken ct);

    /// <summary>指定單位與所有下層中的有效單位。</summary>
    Task<IReadOnlySet<Guid>> SubtreeAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken ct);
}
