namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// AD 目錄裡的一位人員，只帶清單、匯入與同步需要的屬性；其他屬性在檢視單一人員時才讀（<see cref="DirectoryPersonDetail"/>）。
/// <c>AccountName</c> 是 <c>sAMAccountName</c>（不含網域）；<c>DisplayName</c> 在目錄沒填時是帳號名，與登入時的規則相同；
/// <c>Disabled</c> 是 <c>userAccountControl</c> 的停用位元，停用的帳號無法以 AD 登入。
/// </summary>
public sealed record DirectoryPerson(string Sid, string Dn, string AccountName, string DisplayName, string? Email, string? Department, string? Title, bool Disabled,
    string? Upn = null)
{
    /// <summary>所在 OU／容器由上而下（<c>研發處 › 平台部</c>），不含網域與本人的 CN。</summary>
    public string Path => DirectoryNames.PathOf(Dn);
}

/// <summary>
/// 一位人員在 AD 上的完整屬性（管理員檢視用，不寫進平台）。時間都是 UTC；null 表示沒有值或「永不」（帳號、密碼到期）。
/// <c>AccountControl</c> 是 <c>userAccountControl</c> 再併入即時計算的「已鎖定」與「密碼已過期」位元（<c>msDS-User-Account-Control-Computed</c>），
/// 因為存放的 <c>userAccountControl</c> 不會反映這兩種狀態。
/// <c>LastLogon</c>、<c>LogonCount</c>、<c>BadPasswordCount</c>、<c>BadPasswordTime</c> 不在 DC 之間複寫，是這次連到的 DC 上的值；
/// <c>LastLogonTimestamp</c> 會複寫，但預設最多落後 14 天，只能看出「最近是否還在用」。
/// </summary>
public sealed record DirectoryPersonDetail(
    string Sid, string? ObjectGuid, string Dn, string AccountName, string DisplayName, string? GivenName, string? Surname, string? Upn, string? Email,
    string? EmployeeId, string? Description, string? Company, string? Department, string? Title, string? Manager, string? Office, string? Phone, string? Mobile,
    int AccountControl, DateTimeOffset? AccountExpires, DateTimeOffset? PasswordLastSet, bool PasswordMustChange, DateTimeOffset? PasswordExpires,
    DateTimeOffset? LockoutTime, int? BadPasswordCount, DateTimeOffset? BadPasswordTime, DateTimeOffset? LastLogon, DateTimeOffset? LastLogonTimestamp,
    int? LogonCount, DateTimeOffset? WhenCreated, DateTimeOffset? WhenChanged, IReadOnlyList<string> Groups, bool MoreGroups)
{
    /// <summary>所在 OU／容器由上而下，與 <see cref="DirectoryPerson.Path"/> 相同。</summary>
    public string Path => DirectoryNames.PathOf(Dn);
}

/// <summary>目錄裡可放人員的節點（OU 或容器）。<c>People</c> 是節點底下（含子節點）的人數，超過計數上限時為 null。</summary>
public sealed record DirectoryNode(string Dn, string Name, string Kind, int? People, bool HasChildren);

/// <summary><see cref="DirectoryNode.Kind"/> 的值。</summary>
public static class DirectoryNodeKinds
{
    /// <summary>搜尋根（通常是網域）。</summary>
    public const string Root = "root";
    public const string OrganizationalUnit = "ou";
    /// <summary><c>CN=Users</c>、<c>CN=Computers</c> 這類內建容器與 <c>Builtin</c>。</summary>
    public const string Container = "container";
}

/// <summary>
/// 一個節點的直屬子節點；<c>People</c> 是這個節點底下（含子節點）的人數，超過計數上限時為 null。
/// <c>MoreChildren</c> 表示子節點超過上限、只回前面一段。
/// </summary>
public sealed record DirectoryBranch(int? People, IReadOnlyList<DirectoryNode> Children, bool MoreChildren = false);

/// <summary>節點人員；<c>More</c> 表示超過上限只回前面一段（依顯示名稱），畫面請管理員改選下層節點。</summary>
public sealed record DirectoryListing(IReadOnlyList<DirectoryPerson> People, bool More);

/// <summary>
/// 以設定的服務帳號讀取 AD 目錄：只回人員（不含電腦、聯絡人與群組）與可放人員的節點（不含進階檢視才看得到的系統容器）。
/// 未設定服務帳號、無法連線或查詢失敗時拋出 <c>ExternalServiceException</c>（<c>directory_not_configured</c>／<c>directory_unavailable</c>），細節只進日誌。
/// 節點 DN 不存在時回 null；呼叫端負責確認 DN 在搜尋根之下。
/// </summary>
public interface IAdDirectory
{
    /// <summary>
    /// 服務帳號能否寫回 AD 屬性。目前只讀：寫回需要在目標 OU 委派屬性寫入權限，並另外設計稽核與還原，
    /// 實作前畫面以這個旗標顯示「唯讀」。
    /// </summary>
    bool Writable { get; }

    /// <summary>直屬子節點與人數。</summary>
    Task<DirectoryBranch?> ChildrenAsync(string dn, CancellationToken ct);

    /// <summary>
    /// 節點的人員：<paramref name="subtree"/> 為 false 時只列直屬，最多 <paramref name="limit"/> 筆。
    /// <paramref name="query"/> 不為空時只列姓名、帳號、UPN、信箱、部門或職稱含這段文字的人（節點超過上限時在目錄端找人）。
    /// </summary>
    Task<DirectoryListing?> PeopleAsync(string dn, bool subtree, string? query, int limit, CancellationToken ct);

    /// <summary>依 SID 查人；目錄裡找不到（已刪除或不在搜尋根之下）的 SID 不出現在結果中。</summary>
    Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct);

    /// <summary>依 <c>sAMAccountName</c> 精確查人（不分大小寫）；找不到時回 null。改綁 AD 帳號時以管理員輸入的短帳號取得 SID。</summary>
    Task<DirectoryPerson?> FindAccountAsync(string accountName, CancellationToken ct);

    /// <summary>一位人員的完整屬性；目錄裡找不到這個 SID 時回 null。</summary>
    Task<DirectoryPersonDetail?> DetailAsync(string sid, CancellationToken ct);
}
