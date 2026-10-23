namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// AD 目錄裡的一位人員，只帶匯入、同步與管理員檢視需要的屬性。<c>AccountName</c> 是 <c>sAMAccountName</c>（不含網域）；
/// <c>DisplayName</c> 在目錄沒填時是帳號名，與登入時的規則相同；<c>Disabled</c> 是 <c>userAccountControl</c> 的停用位元，停用的帳號無法以 AD 登入。
/// <c>LastLogon</c> 是 <c>lastLogonTimestamp</c>：DC 之間約兩週才複寫一次，只能看出「最近是否還在用」。
/// </summary>
public sealed record DirectoryPerson(string Sid, string Dn, string AccountName, string DisplayName, string? Email, string? Department, string? Title, bool Disabled,
    string? Upn = null, DateTimeOffset? WhenChanged = null, DateTimeOffset? LastLogon = null)
{
    /// <summary>所在 OU／容器由上而下（<c>研發處 › 平台部</c>），不含網域與本人的 CN。</summary>
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

/// <summary>一個節點的直屬子節點；<c>People</c> 是這個節點底下（含子節點）的人數，超過計數上限時為 null。</summary>
public sealed record DirectoryBranch(int? People, IReadOnlyList<DirectoryNode> Children);

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

    /// <summary>節點的人員：<paramref name="subtree"/> 為 false 時只列直屬，最多 <paramref name="limit"/> 筆。</summary>
    Task<DirectoryListing?> PeopleAsync(string dn, bool subtree, int limit, CancellationToken ct);

    /// <summary>依 SID 查人；目錄裡找不到（已刪除或不在搜尋根之下）的 SID 不出現在結果中。</summary>
    Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct);
}
