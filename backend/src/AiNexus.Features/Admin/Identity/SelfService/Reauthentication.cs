using System.Globalization;
using System.Security.Claims;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.SelfService;

/// <summary>
/// 步升驗證（step-up）：敏感的帳號變更前，要求在 <see cref="SelfServiceOptions.ReauthenticationMinutes"/> 內重新證明身分。
/// 成功時把時間寫進目前 session cookie 的 <see cref="SessionIdentity.Reauthenticated"/>；Windows 整合驗證沒有 cookie、
/// 每個請求都由 Kerberos／NTLM 重新驗證，所以直接視為已步升。
/// </summary>
public sealed class Reauthentication(IAdAuthenticator directory, LocalAuthenticator local, AppDbContext db, IOptions<SelfServiceOptions> options,
    IOptions<AdAuthenticationOptions> ad, TimeProvider clock)
{
    /// <summary>這次請求是否由 session cookie 驗證（登入表單或本地密碼）；否則是每個請求都重新驗證的 Windows 身分。</summary>
    public static bool HasSession(HttpContext http) => http.User.FindFirstValue(SessionIdentity.UserId) is not null;

    /// <summary>目前 session 的登入方式：<c>local</c>、<c>ad</c> 或 <c>windows</c>。</summary>
    public string Method(HttpContext http) => http.User.FindFirstValue(SessionIdentity.Method) ?? ad.Value.DirectoryMethod;

    /// <summary>測試身分不能改被測者的帳號安全設定；其餘回傳成功。</summary>
    public static Result Allowed(HttpContext http) => SessionIdentity.IsTest(http.User) ? IdentityErrors.TestAdminReadOnly : Result.Success;

    /// <summary>步升有效到何時；Windows 直接驗證的請求回傳 <see cref="DateTimeOffset.MaxValue"/>，沒有步升過為 null。</summary>
    public DateTimeOffset? FreshUntil(HttpContext http)
    {
        if (!HasSession(http)) return DateTimeOffset.MaxValue;
        if (!long.TryParse(http.User.FindFirstValue(SessionIdentity.Reauthenticated), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return null;
        var until = DateTimeOffset.FromUnixTimeSeconds(seconds).AddMinutes(options.Value.ReauthenticationMinutes);
        return until > clock.GetUtcNow() ? until : null;
    }

    /// <summary>敏感變更的前置檢查：不是測試身分，且步升仍有效。</summary>
    public Result Require(HttpContext http)
    {
        var allowed = Allowed(http);
        if (!allowed.IsSuccess) return allowed;
        return FreshUntil(http) is null ? IdentityErrors.ReauthenticationRequired : Result.Success;
    }

    /// <summary>
    /// 可否用「目前密碼」步升：本地 session 比對本地密碼；AD／Windows session 以 AD 帳密 bind（需設定服務帳號）。
    /// 寄到信箱的驗證碼是否可用由呼叫端判斷（需要已驗證的信箱與可寄信）。
    /// </summary>
    public bool PasswordAvailable(HttpContext http, User user) => Method(http) == "local"
        ? user.LocalEnabled && user.LocalAccount is not null && user.PasswordHash is not null
        : user.AdEnabled && ad.Value.Configured && UserAccounts.DirectoryBound(user);

    /// <summary>
    /// 以目前登入方式的密碼驗證本人。本地密碼沿用 <see cref="LocalAuthenticator"/> 的失敗計數與鎖定；AD 密碼必須驗出同一個 SID，
    /// 成功時順便更新 <see cref="User.DirectoryVerifiedAt"/>。錯誤一律回 <c>reauthentication_failed</c>，不透露是哪一步失敗。
    /// </summary>
    public async Task<Result> VerifyPasswordAsync(HttpContext http, User user, string? password, CancellationToken ct)
    {
        if (!PasswordAvailable(http, user)) return IdentityErrors.ReauthenticationUnavailable;
        if (string.IsNullOrEmpty(password) || password.Length > 1024) return IdentityErrors.ReauthenticationFailed;
        if (Method(http) == "local")
        {
            var verified = await local.AuthenticateAsync(user.LocalAccount!, password, ct);
            return verified.IsSuccess && verified.Value.Id == user.Id ? Result.Success : IdentityErrors.ReauthenticationFailed;
        }
        if (user.AdAccount is null) return IdentityErrors.ReauthenticationFailed;
        var identity = await directory.AuthenticateAsync(user.AdAccount, password, ct);
        if (!identity.IsSuccess || identity.Value.Sid != user.Sid) return IdentityErrors.ReauthenticationFailed;
        user.DirectoryVerifiedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }

    /// <summary>步升成功：重發目前的 session cookie，加上步升時間。沒有 cookie 的 Windows 請求不需要。</summary>
    public Task MarkAsync(HttpContext http, User user) => ReissueAsync(http, user, clock.GetUtcNow());

    /// <summary>
    /// 變更 <see cref="User.SecurityVersion"/> 並儲存後呼叫：以新版本重發目前的 cookie，讓這個 session 保留、其他 session 失效。
    /// 保留原本的步升時間；claims 改變後舊的 CSRF token 會失效，呼叫端要回傳新的 token。
    /// </summary>
    public Task ReissueAsync(HttpContext http, User user)
        => ReissueAsync(http, user, long.TryParse(http.User.FindFirstValue(SessionIdentity.Reauthenticated), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null);

    private async Task ReissueAsync(HttpContext http, User user, DateTimeOffset? reauthenticated)
    {
        if (!HasSession(http)) return;
        var principal = SessionIdentity.Principal(user, Method(http));
        var identity = (ClaimsIdentity)principal.Identity!;
        if (http.User.FindFirstValue(SessionIdentity.ActiveIdentity) is { } active) identity.AddClaim(new(SessionIdentity.ActiveIdentity, active));
        if (reauthenticated is { } at) identity.AddClaim(new(SessionIdentity.Reauthenticated, at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        await AuthSession.ReplaceSessionAsync(http, principal);
    }
}
