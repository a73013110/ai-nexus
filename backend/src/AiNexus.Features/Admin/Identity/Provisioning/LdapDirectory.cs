using System.DirectoryServices.Protocols;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using static AiNexus.Features.Admin.Identity.Authentication.LdapConnections;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// <see cref="IAdDirectory"/> 的 LDAP 實作：以服務帳號 bind（連線規則與登入共用 <see cref="LdapConnections"/>），
/// 用 paged results 控制每次取回的筆數，並要求伺服器依顯示名稱排序（伺服器不支援時照收、在這裡排序）。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim without AvailableWaitHandle holds nothing to release; disposing a shared gate would throw in work still releasing it during shutdown.")]
public sealed partial class LdapDirectory(IOptions<AdAuthenticationOptions> options) : IAdDirectory
{
    private const int SidBatch = 50;
    private const int PageSize = 200;
    private static readonly string[] Attributes = ["objectSid", "sAMAccountName", "displayName", "mail", "department", "title", "userAccountControl"];
    // 與登入的閘門分開：管理員連續搜尋不應排擠同時間的登入。
    private readonly SemaphoreSlim gate = new(2, 2);

    public Task<DirectorySearchResult> SearchAsync(string keyword, int limit, CancellationToken ct) => RunAsync((connection, settings) =>
    {
        if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
        return Search(connection, settings, keyword, limit);
    }, ct);

    public Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct)
    {
        var valid = sids.Where(x => SidPattern().IsMatch(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (valid.Length == 0) return Task.FromResult<IReadOnlyList<DirectoryPerson>>([]);
        return RunAsync<IReadOnlyList<DirectoryPerson>>((connection, settings) =>
        {
            if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
            return Find(connection, settings, valid, ct);
        }, ct);
    }

    /// <summary>
    /// 篩選：人員（<c>objectCategory=person</c> 排除電腦，<c>objectClass=user</c> 排除聯絡人），
    /// 帳號、UPN、信箱、名、姓、部門比對前綴，顯示名稱比對包含（中文姓名常只輸入名字）。關鍵字一律跳脫。
    /// </summary>
    public static string SearchFilter(string keyword)
    {
        var value = EscapeFilter(keyword.Trim());
        return $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName={value}*)(userPrincipalName={value}*)(mail={value}*)(givenName={value}*)(sn={value}*)(department={value}*)(displayName=*{value}*)))";
    }

    /// <summary>依 SID 查人的篩選；AD 接受字串形式的 SID（<c>S-1-5-21-…</c>）。</summary>
    public static string SidFilter(IEnumerable<string> sids)
        => $"(&(objectCategory=person)(objectClass=user)(|{string.Concat(sids.Select(x => $"(objectSid={EscapeFilter(x)})"))}))";

    /// <summary>
    /// DN 轉成由上而下的 OU／容器路徑：<c>CN=林小明,OU=平台部,OU=研發處,DC=example,DC=test</c> → <c>研發處 › 平台部</c>。
    /// 處理 RFC 4514 的跳脫（<c>\,</c> 與 <c>\xx</c> 的 UTF-8 位元組）；去掉第一段（本人）與 DC。
    /// </summary>
    public static string PathOf(string distinguishedName)
    {
        var parts = new List<(string Type, string Value)>();
        var type = new StringBuilder();
        var value = new List<byte>();
        var inValue = false;
        for (var i = 0; i < distinguishedName.Length; i++)
        {
            var c = distinguishedName[i];
            if (c == '\\' && i + 1 < distinguishedName.Length)
            {
                if (i + 2 < distinguishedName.Length && Uri.IsHexDigit(distinguishedName[i + 1]) && Uri.IsHexDigit(distinguishedName[i + 2]))
                {
                    value.Add(Convert.ToByte(distinguishedName.Substring(i + 1, 2), 16));
                    i += 2;
                }
                else value.AddRange(Encoding.UTF8.GetBytes(distinguishedName[++i].ToString()));
            }
            else if (c is ',' or ';') { Flush(); inValue = false; }
            else if (c == '=' && !inValue) inValue = true;
            else if (inValue) value.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
            else type.Append(c);
        }
        Flush();
        return string.Join(" › ", parts.Skip(1)
            .Where(x => x.Type.Equals("OU", StringComparison.OrdinalIgnoreCase) || x.Type.Equals("CN", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value).Reverse());

        void Flush()
        {
            if (type.Length > 0) parts.Add((type.ToString().Trim(), Encoding.UTF8.GetString([.. value]).Trim()));
            type.Clear(); value.Clear();
        }
    }

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
                    // 顯示名稱的包含比對在大型目錄可能較慢，給比登入寬一些的時間；伺服器端另有 TimeLimit。
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
    private static DirectorySearchResult Search(LdapConnection connection, AdAuthenticationOptions settings, string keyword, int limit)
    {
        var request = Request(settings, SearchFilter(keyword));
        // 只取第一頁（limit + 1 筆，多出的一筆用來判斷「還有更多」）；連線隨後關閉，伺服器端的分頁狀態一併釋放。
        request.Controls.Add(new PageResultRequestControl(limit + 1) { IsCritical = false });
        request.Controls.Add(new SortRequestControl("displayName", false) { IsCritical = false });
        var response = (SearchResponse)connection.SendRequest(request);
        var people = Read(response).ToList();
        var more = people.Count > limit || Cookie(response).Length > 0;
        return new([.. people.Take(limit).OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.AccountName, StringComparer.OrdinalIgnoreCase)], more);
    }

    [SupportedOSPlatform("windows")]
    private static List<DirectoryPerson> Find(LdapConnection connection, AdAuthenticationOptions settings, IEnumerable<string> sids, CancellationToken ct)
    {
        var people = new List<DirectoryPerson>();
        foreach (var batch in sids.Chunk(SidBatch))
        {
            var request = Request(settings, SidFilter(batch));
            var paging = new PageResultRequestControl(PageSize) { IsCritical = false };
            request.Controls.Add(paging);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var response = (SearchResponse)connection.SendRequest(request);
                people.AddRange(Read(response));
                var cookie = Cookie(response);
                if (cookie.Length == 0) break;
                paging.Cookie = cookie;
            }
        }
        return people;
    }

    private static SearchRequest Request(AdAuthenticationOptions settings, string filter)
        => new(settings.Base, filter, SearchScope.Subtree, Attributes) { TimeLimit = TimeSpan.FromSeconds(10) };

    private static byte[] Cookie(SearchResponse response) => response.Controls.OfType<PageResultResponseControl>().FirstOrDefault()?.Cookie ?? [];

    [SupportedOSPlatform("windows")]
    private static IEnumerable<DirectoryPerson> Read(SearchResponse response)
    {
        foreach (SearchResultEntry entry in response.Entries)
        {
            if (entry.Attributes["objectSid"]?[0] is not byte[] bytes || Text(entry, "sAMAccountName") is not { } name) continue;
            yield return new(new SecurityIdentifier(bytes, 0).Value, name, Text(entry, "displayName") ?? name, Text(entry, "mail"),
                Text(entry, "department"), Text(entry, "title"), PathOf(entry.DistinguishedName), Disabled(entry));
        }
    }

    private static string? Text(SearchResultEntry entry, string attribute)
        => entry.Attributes[attribute]?[0] is string value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    [GeneratedRegex(@"^S-1-\d{1,3}(-\d{1,10}){1,15}$")]
    private static partial Regex SidPattern();
}
