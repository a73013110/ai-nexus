using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// 匯入或從 AD 更新一位人員的結果；預覽時是「將會」發生的事。<paramref name="Outcome"/> 是 <see cref="DirectoryOutcomes"/> 之一，
/// <paramref name="Changes"/> 是會寫入（或已寫入）的欄位，<paramref name="Enabled"/> 是平台帳號目前是否啟用（尚無帳號時為 null）。
/// </summary>
public sealed record DirectoryOutcomeDto(string Sid, Guid? UserId, string Account, string DisplayName, string? Email, string Path,
    bool DirectoryDisabled, bool? Enabled, string Outcome, IReadOnlyList<DirectoryChange> Changes, string? ErrorCode = null);

public sealed record DirectoryOutcomesDto(IReadOnlyList<DirectoryOutcomeDto> Items);

/// <summary><see cref="DirectoryOutcomeDto.Outcome"/> 的值。前三種會寫入，其餘只回報。</summary>
public static class DirectoryOutcomes
{
    /// <summary>建立新的平台帳號（與首次登入相同：member 角色、AD 登入）。</summary>
    public const string Create = "create";
    /// <summary>綁定管理員預建、尚未登入過的同名帳號。</summary>
    public const string Bind = "bind";
    /// <summary>以目錄資料更新帳號、姓名或信箱。</summary>
    public const string Update = "update";
    public const string Unchanged = "unchanged";
    /// <summary>AD 已停用，未要求一併匯入，所以不建立或綁定。</summary>
    public const string Disabled = "disabled";
    /// <summary>同名的 AD 帳號已連結另一個 SID（<c>ad_binding_conflict</c>）。</summary>
    public const string Conflict = "conflict";
    /// <summary>帳號名不符平台規則（<c>invalid_account</c>）。</summary>
    public const string InvalidAccount = "invalid_account";
    /// <summary>平台帳號已刪除；刪除保留 SID 防止他人接管，不自動復原。</summary>
    public const string Deleted = "deleted";
    /// <summary>目錄找不到這個 SID（已刪除或不在搜尋根之下）。</summary>
    public const string NotFound = "not_found";
    /// <summary>平台帳號沒有綁定 AD（管理員預建、尚未登入或只用本地密碼）。</summary>
    public const string NotLinked = "not_linked";
    /// <summary>寫入時失敗，代碼在 <see cref="DirectoryOutcomeDto.ErrorCode"/>。</summary>
    public const string Failed = "failed";
}
