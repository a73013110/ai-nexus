using System.DirectoryServices.Protocols;
using System.Security.Principal;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using static AiNexus.Features.Admin.Identity.Authentication.LdapConnections;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>以 LDAP 驗證 AD 帳密：先用服務帳號查出使用者的 DN 與 SID，再以使用者自己的密碼 bind 驗證。連線規則在 <see cref="LdapConnections"/>。</summary>
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
        if (!OperatingSystem.IsWindows()) throw PlatformUnavailable();
        using var service = BindService(settings);
        ct.ThrowIfCancellationRequested();
        var filter = $"(&(objectClass=user)({settings.AccountAttribute}={EscapeFilter(account)}))";
        // SizeLimit 設為 2 而非 1：多於一筆就能判定為異常（同名帳號），而不是靜默取第一筆。
        var search = new SearchRequest(settings.Base, filter, SearchScope.Subtree, "objectSid", "sAMAccountName", "displayName", "userAccountControl", "mail")
        {
            SizeLimit = 2,
            TimeLimit = TimeSpan.FromSeconds(5),
        };
        SearchResponse result;
        try { result = (SearchResponse)service.SendRequest(search); }
        catch (DirectoryOperationException error) { throw Unavailable("ad_search_unavailable", "AD 查詢暫時無法使用。", error); }
        if (result.Entries.Count != 1) return IdentityErrors.AdInvalidCredentials;
        var entry = result.Entries[0];
        // 已停用的帳號直接當成帳密錯誤回覆，不透露停用狀態。
        if (Disabled(entry)) return IdentityErrors.AdInvalidCredentials;
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
        var mail = entry.Attributes["mail"]?[0]?.ToString();
        return new AdIdentity(sid, $"{name}@{settings.Domain}", display, string.IsNullOrWhiteSpace(mail) ? null : mail);
    }
}
