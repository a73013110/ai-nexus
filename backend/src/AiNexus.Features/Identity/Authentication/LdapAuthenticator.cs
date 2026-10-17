using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>以 LDAP 驗證 AD 帳密：先用服務帳號查出使用者的 DN 與 SID，再以使用者自己的密碼 bind 驗證。</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim without AvailableWaitHandle holds nothing to release; disposing a shared gate would throw in work still releasing it during shutdown.")]
public sealed class LdapAuthenticator(IOptions<AdAuthenticationOptions> options) : IAdAuthenticator
{
    private readonly SemaphoreSlim gate = new(4, 4);
    public async Task VerifyServiceAsync(CancellationToken ct)
    {
        var settings = Require(options.Value);
        await Task.Run(() => { using var service = BindService(settings); }, ct);
    }
    public async Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        Require(options.Value);
        if (!Regex.IsMatch(account, @"^[\p{L}\p{N}._-]{1,64}$") || string.IsNullOrEmpty(password) || password.Length > 1024)
            return IdentityErrors.AdInvalidCredentials;
        await gate.WaitAsync(ct);
        try { return await Task.Run(() => Authenticate(account, password, ct), ct); }
        finally { gate.Release(); }
    }

    private Result<AdIdentity> Authenticate(string account, string password, CancellationToken ct)
    {
        var settings = options.Value;
        if (!OperatingSystem.IsWindows()) throw Unavailable("ad_platform_unavailable", "此部署的 AD 登入需要 Windows 主機。");
        using var service = BindService(settings);
        ct.ThrowIfCancellationRequested();
        var filter = $"(&(objectClass=user)({settings.AccountAttribute}={EscapeFilter(account)}))";
        // SizeLimit 設為 2 而非 1：多於一筆就能判定為異常（同名帳號），而不是靜默取第一筆。
        var search = new SearchRequest(settings.Base, filter, SearchScope.Subtree, "objectSid", "sAMAccountName", "displayName", "userAccountControl")
        {
            SizeLimit = 2,
            TimeLimit = TimeSpan.FromSeconds(5),
        };
        SearchResponse result;
        try { result = (SearchResponse)service.SendRequest(search); }
        catch (DirectoryOperationException error) { throw Unavailable("ad_search_unavailable", "AD 查詢暫時無法使用。", error); }
        if (result.Entries.Count != 1) return IdentityErrors.AdInvalidCredentials;
        var entry = result.Entries[0];
        // userAccountControl 的 ACCOUNTDISABLE 位元（2）代表帳號已停用；直接當成帳密錯誤回覆，不透露停用狀態。
        if (entry.Attributes["userAccountControl"]?[0] is string flags && int.TryParse(flags, out var value) && (value & 2) != 0)
            return IdentityErrors.AdInvalidCredentials;
        ct.ThrowIfCancellationRequested();
        using var user = Connect(settings, entry.DistinguishedName, password);
        try { user.Bind(); }
        // 錯誤碼 49 為 invalid credentials；其他 LDAP 錯誤代表服務異常，不可當成帳密錯誤，否則會誤導使用者與稽核。
        catch (LdapException ex) when (ex.ErrorCode == 49) { return IdentityErrors.AdInvalidCredentials; }
        catch (LdapException error) { throw Unavailable("ad_unavailable", "AD 服務暫時無法使用，請稍後重試。", error); }
        ct.ThrowIfCancellationRequested();
        if (entry.Attributes["objectSid"]?[0] is not byte[] bytes) return IdentityErrors.AdSidRequired;
        var sid = new SecurityIdentifier(bytes, 0).Value;
        var name = entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? account;
        var display = entry.Attributes["displayName"]?[0]?.ToString() ?? name;
        return new AdIdentity(sid, $"{name}@{settings.Domain}", display);
    }

    private static AdAuthenticationOptions Require(AdAuthenticationOptions settings)
    {
        if (!settings.Configured) throw Unavailable("ad_not_configured", "AD 登入尚未完成設定，請在秘密檔設定 BindPassword。");
        return settings;
    }

    private static LdapConnection BindService(AdAuthenticationOptions settings)
    {
        var service = Connect(settings, settings.BindName, settings.BindPassword);
        try { service.Bind(); return service; }
        catch (LdapException error) { service.Dispose(); throw Unavailable("ad_bind_unavailable", "AD 服務帳號連線失敗，請確認 Server、BindUser 與 BindPassword。", error); }
    }

    private static LdapConnection Connect(AdAuthenticationOptions settings, string user, string password)
    {
        var identifier = new LdapDirectoryIdentifier(settings.Host, settings.Port);
        var connection = new LdapConnection(identifier, new NetworkCredential(user, password), AuthType.Basic) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
            if (settings.UseLdaps) connection.SessionOptions.SecureSocketLayer = true;
            else connection.SessionOptions.StartTransportLayerSecurity(null);
            return connection;
        }
        catch { connection.Dispose(); throw Unavailable("ad_tls_unavailable", "無法建立 AD 加密連線，請確認 StartTLS／LDAPS 與憑證設定。"); }
    }

    /// <summary>依 RFC 4515 跳脫 LDAP 篩選器中的特殊字元，避免帳號名稱被注入成額外的查詢條件。</summary>
    public static string EscapeFilter(string value)
    {
        var escaped = new StringBuilder();
        foreach (var character in value) escaped.Append(character switch { '\\' => "\\5c", '*' => "\\2a", '(' => "\\28", ')' => "\\29", '\0' => "\\00", _ => character.ToString() });
        return escaped.ToString();
    }
    private static ExternalServiceException Unavailable(string code, string detail, Exception? inner = null) => new(Error.Unavailable(code), detail, inner);
}
