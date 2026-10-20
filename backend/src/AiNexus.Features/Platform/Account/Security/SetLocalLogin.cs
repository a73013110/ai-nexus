using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>
/// 啟用本地登入、變更本地帳號名或密碼（需步升）。AD 使用者也可以建立本地密碼作為備援，但受目錄新鮮度規則限制。
/// 完成後遞增 SecurityVersion（其他工作階段登出）、重發目前的 session，並寄通知到已驗證的信箱。
/// </summary>
internal sealed class SetLocalLogin(AccountSecurity security, Reauthentication reauthentication, UserCredentials credentials)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/settings/account/local-login", (LocalLoginRequest body, HttpContext http, ICurrentUser user, SetLocalLogin handler, CancellationToken ct)
            => handler.HandleAsync(http, user.User, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(AccountModule.CredentialsRateLimit)
        .WithName("SetLocalLogin");

    public async Task<Result<AccountSecurityChangeDto>> HandleAsync(HttpContext http, User user, LocalLoginRequest body, CancellationToken ct)
    {
        var fresh = reauthentication.Require(http);
        if (!fresh.IsSuccess) return fresh.Error;
        var notify = AccountSecurity.VerifiedEmail(user);
        var change = await credentials.SetLocalLoginAsync(user, body.Account, body.Password, ct);
        if (!change.IsSuccess) return change.Error;
        var (action, text) = change.Value switch
        {
            { Enabled: true } => ("account.local_login_enabled", "啟用了本地登入"),
            { Renamed: true } => ("account.local_account_renamed", "變更了本地登入帳號與密碼"),
            _ => ("account.password_changed", "變更了本地登入密碼"),
        };
        security.Audit(user, action, new { localAccount = user.LocalAccount });
        await security.NotifyAsync(user, notify, text, $"本地登入帳號：{user.LocalAccount}", ct);
        return await security.CommitAsync(http, user, ct);
    }
}
