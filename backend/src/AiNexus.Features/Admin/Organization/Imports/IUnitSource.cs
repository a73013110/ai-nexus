using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Organization.Imports;

/// <summary>
/// 匯入部門的來源（公文系統 GD 的部門表，由 Integrations 實作）。Organization 定義、上層實作：GD 的連線、帳號對應與錯誤分類都在 Integrations，
/// 而 Integrations 依賴對話、成果等業務模組；反過來讓 Organization 依賴它，業務模組日後要用組織範圍就會成環（docs/architecture/module-boundaries.md）。
/// </summary>
public interface IUnitSource
{
    /// <summary>
    /// 讀取來源的全部部門，主管已對到使用者。來源不可用時回 <see cref="OrganizationErrors.ImportNotConfigured"/>、
    /// <see cref="OrganizationErrors.ImportUnavailable"/> 或 <see cref="OrganizationErrors.ImportRejected"/>，不拋例外。
    /// </summary>
    Task<Result<IReadOnlyList<SourceUnit>>> ReadAsync(CancellationToken ct);
}

/// <summary>
/// 來源的一個部門，原樣帶回（名稱未去空白、上層可能是 0 或不存在的編號），規則由 <see cref="UnitImportPlan"/> 統一判斷。
/// </summary>
/// <param name="Key">來源主鍵（GD 的 <c>DEP_NO</c>），存進 <c>Units.GdDepNo</c>。</param>
/// <param name="Name">名稱，未去空白，可能是 null。</param>
/// <param name="ParentKey">上層的來源主鍵；null、0 或不存在的編號都視為最上層。</param>
/// <param name="SortOrder">來源的排列順序，只用來決定同層的先後。</param>
/// <param name="Disabled">來源已關閉這個部門。</param>
/// <param name="Managers">主管在前、副主管在後，已去重。</param>
public sealed record SourceUnit(int Key, string? Name, int? ParentKey, int? SortOrder, bool Disabled, IReadOnlyList<SourceManager> Managers);

/// <summary>來源列的一位主管：<paramref name="SourceId"/> 是來源的帳號（GD 的 <c>Sys_User.ID</c>）；對不到使用者時 <paramref name="UserId"/> 為 null，原因在 <paramref name="Issue"/>。</summary>
public sealed record SourceManager(string SourceId, Guid? UserId, SourceAccountIssue? Issue, bool Deputy);

/// <summary>來源帳號對不到使用者的原因。</summary>
public enum SourceAccountIssue
{
    /// <summary>來源的帳號表沒有這個帳號。</summary>
    AccountNotFound,

    /// <summary>來源帳號已停用：人多半已離職或換了帳號，不當成主管。</summary>
    AccountDisabled,

    /// <summary>沒有未刪除的使用者的 AD 帳號對得上（多半是還沒登入過平台）。</summary>
    UserNotFound,

    /// <summary>不只一位使用者對得上，不猜。</summary>
    UserAmbiguous,
}
