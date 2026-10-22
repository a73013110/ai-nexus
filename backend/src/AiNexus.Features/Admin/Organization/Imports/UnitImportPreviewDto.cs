namespace AiNexus.Features.Admin.Organization.Imports;

/// <summary>
/// 從公文系統匯入的預覽（不寫入）。套用時把 <c>Fingerprint</c> 送回：伺服器重讀 GD 與本地資料、重算一次，
/// 結果與預覽不同（期間 GD 或組織有人改過）就拒絕，不會套用使用者沒看過的變更。
/// </summary>
/// <param name="Fingerprint">預覽內容的雜湊，套用時送回。</param>
/// <param name="SourceUnits">GD 的部門總數（含略過的列）。</param>
/// <param name="Units">要新增、綁定、更新或停用的單位，依動作與路徑排列。</param>
/// <param name="Managers">主管的變動；新設的主管若還不是成員會一併加入。</param>
/// <param name="Issues">略過的列與對不到使用者的主管帳號。</param>
public sealed record UnitImportPreviewDto(
    string Fingerprint,
    int SourceUnits,
    IReadOnlyList<UnitImportChangeDto> Units,
    IReadOnlyList<UnitImportManagerDto> Managers,
    IReadOnlyList<UnitImportIssueDto> Issues);

/// <summary>單位的變動。</summary>
public enum UnitImportAction
{
    /// <summary>新增單位。</summary>
    Create,

    /// <summary>綁定同一上層、同名的手建單位，之後由匯入維護。</summary>
    Adopt,

    /// <summary>已綁定的單位依 GD 更新。</summary>
    Update,

    /// <summary>GD 已沒有這個部門：停用，保留成員與歷史。</summary>
    Disable,
}

/// <summary>單位被匯入改到的欄位。</summary>
public enum UnitImportField
{
    Name,
    Parent,
    Enabled,
    SortOrder,
}

/// <summary>
/// 一個單位的變動。<c>Path</c> 是匯入後由最上層到它的名稱；<c>PreviousPath</c> 是匯入前的路徑，只在改名或搬移時有值。
/// <c>UnitId</c> 在新增時為 null（套用時才產生）。<c>Enabled</c> 是匯入後的狀態。
/// </summary>
public sealed record UnitImportChangeDto(
    int GdDepNo,
    Guid? UnitId,
    UnitImportAction Action,
    IReadOnlyList<string> Path,
    IReadOnlyList<string>? PreviousPath,
    bool Enabled,
    IReadOnlyList<UnitImportField> Fields);

/// <summary>
/// 一筆主管的變動：<c>Added</c> 為 true 是設為主管（<c>JoinsUnit</c> 表示此人原本不是成員、會一併加入），false 是取消主管但保留成員。
/// <c>Deputy</c> 表示在 GD 是副主管；本系統兩者都是主管（<c>IsManager</c>），只在預覽標示。
/// </summary>
public sealed record UnitImportManagerDto(
    int GdDepNo,
    Guid? UnitId,
    IReadOnlyList<string> Path,
    Guid UserId,
    string Account,
    string DisplayName,
    bool Added,
    bool Deputy,
    bool JoinsUnit);

/// <summary>匯入時略過或無法處理的原因。</summary>
public enum UnitImportIssueCode
{
    /// <summary>名稱空白，整列略過。</summary>
    NameMissing,

    /// <summary>名稱超過 120 字或含控制字元，整列略過。</summary>
    NameInvalid,

    /// <summary>同一個上層已有同名單位（手建的或另一個 GD 部門），整列略過。</summary>
    NameDuplicate,

    /// <summary>上層關係成環，整列略過。</summary>
    Cycle,

    /// <summary>放上去會超過八層（含掛在底下的手建單位），整列略過。</summary>
    DepthExceeded,

    /// <summary>上層被略過、本地也還沒有這個上層，整列略過。</summary>
    ParentSkipped,

    /// <summary>主管帳號在 GD 的帳號表不存在。</summary>
    AccountNotFound,

    /// <summary>主管的 GD 帳號已停用。</summary>
    AccountDisabled,

    /// <summary>主管帳號對不到平台的使用者（多半還沒登入過）。</summary>
    UserNotFound,

    /// <summary>主管帳號對到不只一位使用者。</summary>
    UserAmbiguous,
}

/// <summary>
/// 一個問題。略過整列時 <c>GdUserId</c> 為 null；主管對不到時是該 GD 帳號，該單位的其他變動照常套用。
/// <c>Name</c> 是 GD 的部門名稱（已去空白，可能為 null）。
/// </summary>
public sealed record UnitImportIssueDto(int GdDepNo, string? Name, UnitImportIssueCode Code, string? GdUserId);

/// <summary>套用前送回預覽的 <c>Fingerprint</c>。</summary>
public sealed record UnitImportRequest(string Fingerprint);

/// <summary>套用的結果摘要（數量），給提示與稽核。</summary>
public sealed record UnitImportResultDto(int Created, int Adopted, int Updated, int Disabled, int ManagersAdded, int ManagersRemoved, int Issues);
