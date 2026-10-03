using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using AiNexus.BuildingBlocks;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Identity;

public sealed class AdAuthenticationOptions
{
    public string Mode { get; set; } = "Ldap";
    public string Url { get; set; } = "";
    public string DnUser { get; set; } = "";
    public string DnPass { get; set; } = "";
    public string AdAccountAttrName { get; set; } = "sAMAccountName";
    public string Domain { get; set; } = "";
    public bool Configured => !string.IsNullOrWhiteSpace(DnPass);
}

public sealed record AdIdentity(string Sid, string Account, string DisplayName);
public interface IAdAuthenticator
{
    Task<AdIdentity> AuthenticateAsync(string account, string password, CancellationToken ct);
    Task VerifyServiceAsync(CancellationToken ct);
}

public sealed class LdapAuthenticator(IOptions<AdAuthenticationOptions> options) : IAdAuthenticator
{
    private readonly SemaphoreSlim gate = new(4, 4);
    public async Task VerifyServiceAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Configured) throw new ApiException(503, "ad_not_configured", "AD DnPass 尚未設定。");
        await Task.Run(() => {
            var raw = settings.Url.Contains("://", StringComparison.Ordinal) ? settings.Url : "ldap://" + settings.Url;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme is not ("ldap" or "ldaps")) throw new ApiException(503, "ad_invalid_configuration", "AD URL 不正確。");
            using var service = Connect(uri, settings.DnUser, settings.DnPass);
            try { service.Bind(); }
            catch (LdapException) { throw new ApiException(503, "ad_bind_unavailable", "AD 服務帳號或目錄連線無法驗證。"); }
        }, ct);
    }
    public async Task<AdIdentity> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        if (!options.Value.Configured) throw new ApiException(503, "ad_not_configured", "AD 登入尚未完成設定，請設定後端 DnPass。");
        if (!Regex.IsMatch(account, @"^[\p{L}\p{N}._-]{1,64}$") || string.IsNullOrEmpty(password) || password.Length > 1024) throw InvalidCredentials();
        await gate.WaitAsync(ct);
        try { return await Task.Run(() => Authenticate(account, password, ct), ct); }
        finally { gate.Release(); }
    }

    private AdIdentity Authenticate(string account, string password, CancellationToken ct)
    {
        var settings = options.Value;
        if (!OperatingSystem.IsWindows()) throw new ApiException(503, "ad_platform_unavailable", "此部署的 AD 登入需要 Windows 主機。");
        var raw = settings.Url.Contains("://", StringComparison.Ordinal) ? settings.Url : "ldap://" + settings.Url;
        var attribute = WebUtility.HtmlDecode(settings.AdAccountAttrName).Trim();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var url) || url.Scheme is not ("ldap" or "ldaps") || string.IsNullOrEmpty(url.Host) || url.AbsolutePath.Length < 2 || !Regex.IsMatch(attribute, "^[A-Za-z][A-Za-z0-9-]{0,63}$"))
            throw new ApiException(503, "ad_invalid_configuration", "AD URL 或帳號欄位設定不正確。");
        var root = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/'));
        using var service = Connect(url, settings.DnUser, settings.DnPass);
        try { service.Bind(); }
        catch (LdapException) { throw new ApiException(503, "ad_bind_unavailable", "AD 服務帳號連線失敗，請確認後端設定。"); }
        ct.ThrowIfCancellationRequested();
        var search = new SearchRequest(root, $"(&(objectClass=user)({attribute}={EscapeFilter(account)}))", SearchScope.Subtree, "objectSid", "sAMAccountName", "displayName", "userAccountControl") { SizeLimit = 2, TimeLimit = TimeSpan.FromSeconds(5) };
        SearchResponse result;
        try { result = (SearchResponse)service.SendRequest(search); }
        catch (DirectoryOperationException) { throw new ApiException(503, "ad_search_unavailable", "AD 查詢暫時無法使用。"); }
        if (result.Entries.Count != 1) throw InvalidCredentials();
        var entry = result.Entries[0];
        if (entry.Attributes["userAccountControl"]?[0] is string flags && int.TryParse(flags, out var value) && (value & 2) != 0) throw InvalidCredentials();
        ct.ThrowIfCancellationRequested();
        using var user = Connect(url, entry.DistinguishedName, password);
        try { user.Bind(); }
        catch (LdapException ex) when (ex.ErrorCode == 49) { throw InvalidCredentials(); }
        catch (LdapException) { throw new ApiException(503, "ad_unavailable", "AD 服務暫時無法使用，請稍後重試。"); }
        ct.ThrowIfCancellationRequested();
        if (entry.Attributes["objectSid"]?[0] is not byte[] bytes) throw new ApiException(403, "ad_sid_required", "AD 帳號缺少 SID，無法建立工作空間。");
        var sid = new SecurityIdentifier(bytes, 0).Value;
        var name = entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? account;
        var display = entry.Attributes["displayName"]?[0]?.ToString() ?? name;
        return new(sid, $"{name}@{settings.Domain}", display);
    }

    private static LdapConnection Connect(Uri url, string dn, string password)
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(url.Host, url.IsDefaultPort ? url.Scheme == "ldaps" ? 636 : 389 : url.Port), new NetworkCredential(dn, password), AuthType.Basic) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
            if (url.Scheme == "ldaps") connection.SessionOptions.SecureSocketLayer = true;
            else connection.SessionOptions.StartTransportLayerSecurity(null);
            return connection;
        }
        catch { connection.Dispose(); throw new ApiException(503, "ad_tls_unavailable", "無法建立 AD 加密連線，請確認 StartTLS／LDAPS 與憑證設定。"); }
    }

    public static string EscapeFilter(string value)
    {
        var escaped = new StringBuilder();
        foreach (var character in value) escaped.Append(character switch { '\\' => "\\5c", '*' => "\\2a", '(' => "\\28", ')' => "\\29", '\0' => "\\00", _ => character.ToString() });
        return escaped.ToString();
    }
    private static ApiException InvalidCredentials() => new(401, "ad_invalid_credentials", "AD 帳號或密碼不正確，或此帳號無法登入。");
}
