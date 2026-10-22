namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// AD 目錄裡的一位人員，只帶匯入與同步需要的屬性。<c>AccountName</c> 是 <c>sAMAccountName</c>（不含網域）；
/// <c>DisplayName</c> 在目錄沒填時是帳號名，與登入時的規則相同；<c>Path</c> 是所在 OU／容器由上而下（<c>研發處 › 平台部</c>），
/// 不含網域與本人的 CN；<c>Disabled</c> 是 <c>userAccountControl</c> 的停用位元，停用的帳號無法以 AD 登入。
/// </summary>
public sealed record DirectoryPerson(string Sid, string AccountName, string DisplayName, string? Email, string? Department, string? Title, string Path, bool Disabled);

/// <summary>目錄搜尋結果；<c>More</c> 表示還有更多符合的人，畫面請使用者縮小關鍵字，而不是翻頁。</summary>
public sealed record DirectorySearchResult(IReadOnlyList<DirectoryPerson> People, bool More);

/// <summary>
/// 以設定的服務帳號讀取 AD 目錄（只讀）：只回人員（不含電腦、聯絡人與群組）。
/// 未設定服務帳號、無法連線或查詢失敗時拋出 <c>ExternalServiceException</c>（<c>directory_not_configured</c>／<c>directory_unavailable</c>），細節只進日誌。
/// </summary>
public interface IAdDirectory
{
    /// <summary>依帳號、UPN、信箱、名、姓、部門前綴或顯示名稱包含關鍵字搜尋，最多 <paramref name="limit"/> 筆。</summary>
    Task<DirectorySearchResult> SearchAsync(string keyword, int limit, CancellationToken ct);

    /// <summary>依 SID 查人；目錄裡找不到（已刪除或不在搜尋根之下）的 SID 不出現在結果中。</summary>
    Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct);
}
