namespace AiNexus.Features.Admin.AccessControl.Scopes;

/// <summary>使用者目前所屬的有效單位，以及其中組織管理勾選為主管的單位（主管範圍只看後者）。</summary>
public sealed record UnitsOfUser(IReadOnlyList<Guid> UnitIds, IReadOnlyList<Guid> ManagedUnitIds);

/// <summary>
/// 計算 <c>unit</c>、<c>unit-tree</c> 與主管範圍需要的組織資料。由 Organization 實作（<c>OrganizationDirectory</c>），同 <see cref="IRequestActor"/> 的做法：
/// AccessControl 被所有模組引用，反過來依賴 Organization 會成環（ADR 0017）。只回答「現在」的有效單位，停用的單位連同下層都不算。
/// </summary>
public interface IUnitMembership
{
    /// <summary>
    /// 使用者目前所屬的有效單位與任主管的有效單位，一次讀出：群組範圍與主管範圍同時需要時不必多查一次。帳號停用或刪除時都為空。
    /// </summary>
    Task<UnitsOfUser> UnitsOfAsync(Guid userId, CancellationToken ct);

    /// <summary>指定單位與所有下層中的有效單位。</summary>
    Task<IReadOnlySet<Guid>> SubtreeAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken ct);
}
