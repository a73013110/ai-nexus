using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using static AiNexus.Features.Admin.Identity.Authentication.LdapConnections;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// <see cref="IAdDirectory"/> 的 LDAP 實作：以服務帳號 bind（連線規則與登入共用 <see cref="LdapConnections"/>），
/// 用 paged results 控制每次取回的筆數。分頁游標綁在連線上，所以每次呼叫在同一條連線內讀完，不跨 HTTP 請求翻頁。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim without AvailableWaitHandle holds nothing to release; disposing a shared gate would throw in work still releasing it during shutdown.")]
public sealed partial class LdapDirectory(IOptions<AdAuthenticationOptions> options) : IAdDirectory
{
    /// <summary>人員：<c>objectCategory=person</c> 排除電腦，<c>objectClass=user</c> 排除聯絡人。</summary>
    public const string PersonFilter = "(&(objectCategory=person)(objectClass=user))";

    /// <summary>
    /// 可放人員的節點：OU、容器（<c>CN=Users</c>）與 <c>Builtin</c>。排除 <c>showInAdvancedViewOnly</c> 的系統容器
    /// （System、Program Data、LostAndFound…），與「Active Directory 使用者和電腦」預設看到的一致。
    /// </summary>
    public const string NodeFilter = "(&(|(objectClass=organizationalUnit)(objectClass=container)(objectClass=builtinDomain))(!(showInAdvancedViewOnly=TRUE)))";

    private const int SidBatch = 50;
    private const int PageSize = 200;
    // 計人數只取 DN，一頁可以大一些（AD 預設 MaxPageSize 是 1000）。
    private const int CountPageSize = 1000;
    // 子節點人數與「是否還有下層」靠一次子樹查詢在這裡分組；超過上限的節點不顯示人數，避免展開網域根時拉回整個大型目錄。
    private const int MaxCounted = 20_000;
    private const int MaxChildren = 500;
    private const int MaxCountedChildren = 50;
    // 檢視單一人員時列出的群組上限；超過時只回前面一段（AD 本身超過 1500 個值也要分段讀取）。
    private const int MaxGroups = 200;
    private static readonly string[] Attributes =
        ["objectSid", "sAMAccountName", "displayName", "mail", "department", "title", "userAccountControl", "userPrincipalName"];
    private static readonly string[] DetailAttributes =
    [
        .. Attributes, "objectGUID", "givenName", "sn", "employeeID", "description", "company", "manager", "physicalDeliveryOfficeName", "telephoneNumber",
        "mobile", "accountExpires", "pwdLastSet", "lockoutTime", "badPwdCount", "badPasswordTime", "lastLogon", "lastLogonTimestamp", "logonCount",
        "whenCreated", "whenChanged", "memberOf",
    ];
    // 即時計算的屬性（鎖定、密碼過期與到期時間）只能以 base 範圍查詢單一物件，不能放進子樹查詢。
    private static readonly string[] ComputedAttributes = ["msDS-User-Account-Control-Computed", "msDS-UserPasswordExpiryTimeComputed"];
    private const int Lockout = 0x10;
    private const int PasswordExpired = 0x80_0000;
    private static readonly string[] NoAttributes = ["1.1"];
    // 與畫面上節點內搜尋相同的欄位，目錄端找人與畫面篩選的結果才一致。
    private static readonly string[] SearchedAttributes = ["displayName", "sAMAccountName", "userPrincipalName", "mail", "department", "title"];
    // 與登入的閘門分開：管理員連續瀏覽不應排擠同時間的登入。
    private readonly SemaphoreSlim gate = new(2, 2);

    public bool Writable => false;

    public Task<DirectoryBranch?> ChildrenAsync(string dn, CancellationToken ct) => RunAsync((connection, _) =>
    {
        if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
        return Children(connection, dn, ct);
    }, ct);

    public Task<DirectoryListing?> PeopleAsync(string dn, bool subtree, string? query, int limit, CancellationToken ct) => RunAsync((connection, _) =>
    {
        if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
        return People(connection, dn, subtree, query, limit, ct);
    }, ct);

    /// <summary>人員篩選；有關鍵字時加上「任一欄位包含」，關鍵字一律跳脫，不能改寫篩選結構。</summary>
    public static string PeopleFilter(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return PersonFilter;
        var words = EscapeFilter(query.Trim());
        return $"(&{PersonFilter}(|{string.Concat(SearchedAttributes.Select(x => $"({x}=*{words}*)"))}))";
    }

    public Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct)
    {
        var valid = sids.Where(x => SidPattern().IsMatch(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (valid.Length == 0) return Task.FromResult<IReadOnlyList<DirectoryPerson>>([]);
        return RunAsync<IReadOnlyList<DirectoryPerson>>((connection, settings) =>
        {
            if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
            return Find(connection, settings.Base, valid, ct);
        }, ct);
    }

    public Task<DirectoryPersonDetail?> DetailAsync(string sid, CancellationToken ct)
    {
        if (!SidPattern().IsMatch(sid)) return Task.FromResult<DirectoryPersonDetail?>(null);
        return RunAsync((connection, settings) =>
        {
            if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
            return Detail(connection, settings.Base, sid);
        }, ct);
    }

    /// <summary>
    /// 存放的 <c>userAccountControl</c> 併入即時計算的「已鎖定」與「密碼已過期」：AD 不會把這兩個狀態寫回存放的值，
    /// 只看存放的值會把鎖定中的帳號顯示成正常。
    /// </summary>
    public static int AccountControl(int stored, int? computed) => stored | ((computed ?? 0) & (Lockout | PasswordExpired));

    /// <summary>依 SID 查人的篩選；AD 接受字串形式的 SID（<c>S-1-5-21-…</c>）。</summary>
    public static string SidFilter(IEnumerable<string> sids)
        => $"(&{PersonFilter}(|{string.Concat(sids.Select(x => $"(objectSid={EscapeFilter(x)})"))}))";

    /// <summary><c>whenChanged</c> 的 GeneralizedTime（<c>20261013013002.0Z</c>）；格式不合時為 null。</summary>
    public static DateTimeOffset? GeneralizedTime(string? value)
        => value is { Length: >= 14 } && DateTime.TryParseExact(value[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time) ? new DateTimeOffset(time, TimeSpan.Zero) : null;

    /// <summary>
    /// FILETIME 屬性（<c>lastLogon</c>、<c>pwdLastSet</c>、<c>accountExpires</c>…，100 奈秒刻度）。0 表示沒有（從未登入、未鎖定），
    /// <c>0x7FFFFFFFFFFFFFFF</c> 表示永不（帳號或密碼不到期），兩者都回 null。
    /// </summary>
    public static DateTimeOffset? FileTime(string? value)
        => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks > 0 && ticks <= DateTime.MaxValue.ToFileTimeUtc()
            ? DateTimeOffset.FromFileTime(ticks).ToUniversalTime() : null;

    private async Task<T> RunAsync<T>(Func<LdapConnection, AdAuthenticationOptions, T> work, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Configured) throw new ExternalServiceException(IdentityErrors.DirectoryNotConfigured, "AD 目錄查詢需要服務帳號，請在秘密檔設定 BindUser 與 BindPassword。");
        await gate.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = BindService(settings);
                    // 子樹計數在大型目錄可能較慢，給比登入寬一些的時間；伺服器端另有 TimeLimit。
                    connection.Timeout = TimeSpan.FromSeconds(15);
                    return work(connection, settings);
                }
                catch (ExternalServiceException error) { throw new ExternalServiceException(IdentityErrors.DirectoryUnavailable, $"AD 目錄查詢失敗（{error.Error.Code}）。", error); }
                catch (DirectoryException error) { throw new ExternalServiceException(IdentityErrors.DirectoryUnavailable, "AD 目錄查詢失敗。", error); }
            }, ct);
        }
        finally { gate.Release(); }
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryBranch? Children(LdapConnection connection, string dn, CancellationToken ct)
    {
        if (Collect(connection, dn, Request(dn, NodeFilter, SearchScope.OneLevel, ["name", "objectClass"]), MaxChildren, PageSize, ct) is not { } found) return null;
        var nodes = found.Entries;
        var nested = Collect(connection, Request(dn, NodeFilter, SearchScope.Subtree, NoAttributes), MaxCounted, CountPageSize, ct);
        var people = Collect(connection, Request(dn, PersonFilter, SearchScope.Subtree, NoAttributes), MaxCounted, CountPageSize, ct);
        // 整層一起數超過上限時（常見是某一個 OU 特別大），改成逐一子節點各自數，大的那個標成 null，其他兄弟仍有人數；
        // 子節點太多時只數前面幾個，避免一次展開發出上百個查詢。
        var separately = 0;
        int? CountOf(string child)
        {
            if (!people.More) return people.Entries.Count(x => DirectoryNames.Within(x.DistinguishedName, child));
            if (++separately > MaxCountedChildren) return null;
            var own = Collect(connection, Request(child, PersonFilter, SearchScope.Subtree, NoAttributes), MaxCounted, CountPageSize, ct);
            return own.More ? null : own.Entries.Count;
        }
        var children = nodes.Select(node =>
        {
            var name = node.Attributes["name"]?[0] as string ?? DirectoryNames.NameOf(node.DistinguishedName);
            var ou = node.Attributes["objectClass"]?.GetValues(typeof(string)).Cast<string>().Any(x => x.Equals("organizationalUnit", StringComparison.OrdinalIgnoreCase)) == true;
            var count = CountOf(node.DistinguishedName);
            // 下層節點超過計數上限時無從判斷，一律當作可展開；展開後是空的也只是多一次查詢。
            var hasChildren = nested.More || nested.Entries.Any(x => DirectoryNames.Below(x.DistinguishedName, node.DistinguishedName));
            return new DirectoryNode(node.DistinguishedName, name, ou ? DirectoryNodeKinds.OrganizationalUnit : DirectoryNodeKinds.Container, count, hasChildren);
        }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return new(people.More ? null : people.Entries.Count, children, found.More);
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryListing? People(LdapConnection connection, string dn, bool subtree, string? query, int limit, CancellationToken ct)
    {
        var request = Request(dn, PeopleFilter(query), subtree ? SearchScope.Subtree : SearchScope.OneLevel, Attributes);
        // 要求伺服器依顯示名稱排序，超過上限時截掉的是名單後段；伺服器不支援時照收、在這裡排序。
        request.Controls.Add(new SortRequestControl("displayName", false) { IsCritical = false });
        if (Collect(connection, dn, request, limit, PageSize, ct) is not var (entries, more)) return null;
        return new([.. Read(entries).OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.AccountName, StringComparer.OrdinalIgnoreCase)], more);
    }

    [SupportedOSPlatform("windows")]
    private static List<DirectoryPerson> Find(LdapConnection connection, string root, string[] sids, CancellationToken ct)
    {
        var people = new List<DirectoryPerson>();
        foreach (var batch in sids.Chunk(SidBatch))
            people.AddRange(Read(Collect(connection, Request(root, SidFilter(batch), SearchScope.Subtree, Attributes), int.MaxValue, PageSize, ct).Entries));
        return people;
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryPersonDetail? Detail(LdapConnection connection, string root, string sid)
    {
        var response = (SearchResponse)connection.SendRequest(Request(root, SidFilter([sid]), SearchScope.Subtree, DetailAttributes));
        if (response.Entries.Count == 0 || response.Entries[0] is not { } entry || Read([entry]).FirstOrDefault() is not { } person) return null;
        var computed = ((SearchResponse)connection.SendRequest(Request(entry.DistinguishedName, "(objectClass=*)", SearchScope.Base, ComputedAttributes)))
            .Entries.Cast<SearchResultEntry>().FirstOrDefault();
        // 群組超過 1500 個時 AD 以 memberOf;range=0-1499 分段回傳；只列前面一段並標示還有更多。
        var memberOf = entry.Attributes.AttributeNames.Cast<string>().FirstOrDefault(x => x.StartsWith("memberOf", StringComparison.OrdinalIgnoreCase));
        var groups = memberOf is null ? [] : entry.Attributes[memberOf].GetValues(typeof(string)).Cast<string>().Select(DirectoryNames.NameOf)
            .Order(StringComparer.OrdinalIgnoreCase).ToList();
        var pwdLastSet = Text(entry, "pwdLastSet");
        return new(person.Sid, entry.Attributes["objectGUID"]?[0] is byte[] { Length: 16 } guid ? new Guid(guid).ToString() : null, person.Dn, person.AccountName,
            person.DisplayName, Text(entry, "givenName"), Text(entry, "sn"), person.Upn, person.Email, Text(entry, "employeeID"), Text(entry, "description"),
            Text(entry, "company"), person.Department, person.Title, Text(entry, "manager") is { } manager ? DirectoryNames.NameOf(manager) : null,
            Text(entry, "physicalDeliveryOfficeName"), Text(entry, "telephoneNumber"), Text(entry, "mobile"),
            AccountControl(Number(entry, "userAccountControl") ?? 0, computed is null ? null : Number(computed, "msDS-User-Account-Control-Computed")),
            FileTime(Text(entry, "accountExpires")), FileTime(pwdLastSet), pwdLastSet == "0",
            computed is null ? null : FileTime(Text(computed, "msDS-UserPasswordExpiryTimeComputed")), FileTime(Text(entry, "lockoutTime")),
            Number(entry, "badPwdCount"), FileTime(Text(entry, "badPasswordTime")), FileTime(Text(entry, "lastLogon")), FileTime(Text(entry, "lastLogonTimestamp")),
            Number(entry, "logonCount"), GeneralizedTime(Text(entry, "whenCreated")), GeneralizedTime(Text(entry, "whenChanged")),
            [.. groups.Take(MaxGroups)], groups.Count > MaxGroups || memberOf?.Contains(';', StringComparison.Ordinal) == true);
    }

    /// <summary>以節點為搜尋根的第一個查詢：節點已被刪除或改名（畫面上的樹是舊的）回 null，其他目錄錯誤照常往外拋。</summary>
    private static (List<SearchResultEntry> Entries, bool More)? Collect(LdapConnection connection, string dn, SearchRequest request, int cap, int pageSize, CancellationToken ct)
    {
        try { return Collect(connection, request, cap, pageSize, ct); }
        catch (DirectoryOperationException error) when (error.Response?.ResultCode == ResultCode.NoSuchObject && request.DistinguishedName == dn) { return null; }
    }

    /// <summary>
    /// 在同一條連線上依 paged results 讀到 <paramref name="cap"/> 筆為止；多讀到的那一筆用來判斷 <c>More</c>。
    /// 提前停下時連線隨即關閉，伺服器端的分頁狀態一併釋放。
    /// </summary>
    private static (List<SearchResultEntry> Entries, bool More) Collect(LdapConnection connection, SearchRequest request, int cap, int pageSize, CancellationToken ct)
    {
        var entries = new List<SearchResultEntry>();
        var paging = new PageResultRequestControl(cap < pageSize ? cap + 1 : pageSize) { IsCritical = false };
        request.Controls.Add(paging);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var response = (SearchResponse)connection.SendRequest(request);
            entries.AddRange(response.Entries.Cast<SearchResultEntry>());
            if (entries.Count > cap) return (entries.GetRange(0, cap), true);
            var cookie = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault()?.Cookie ?? [];
            if (cookie.Length == 0) return (entries, false);
            paging.Cookie = cookie;
        }
    }

    private static SearchRequest Request(string dn, string filter, SearchScope scope, string[] attributes)
        => new(dn, filter, scope, attributes) { TimeLimit = TimeSpan.FromSeconds(10) };

    [SupportedOSPlatform("windows")]
    private static IEnumerable<DirectoryPerson> Read(IEnumerable<SearchResultEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Attributes["objectSid"]?[0] is not byte[] bytes || Text(entry, "sAMAccountName") is not { } name) continue;
            yield return new(new SecurityIdentifier(bytes, 0).Value, entry.DistinguishedName, name, Text(entry, "displayName") ?? name, Text(entry, "mail"),
                Text(entry, "department"), Text(entry, "title"), Disabled(entry), Text(entry, "userPrincipalName"));
        }
    }

    private static string? Text(SearchResultEntry entry, string attribute)
        => entry.Attributes[attribute]?[0] is string value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static int? Number(SearchResultEntry entry, string attribute)
        => int.TryParse(Text(entry, attribute), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;

    [GeneratedRegex(@"^S-1-\d{1,3}(-\d{1,10}){1,15}$")]
    private static partial Regex SidPattern();
}
