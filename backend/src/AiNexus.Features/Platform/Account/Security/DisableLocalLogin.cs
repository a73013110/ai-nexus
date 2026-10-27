using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>
/// 停用本地登入並清除密碼（需步升）；本地是唯一登入方式時拒絕。目前若正是以本地密碼登入，這個工作階段無法再延續，直接登出並在回應標示。
/// </summary>
internal sealed class DisableLocalLogin(AppDbContext db, AccountSecurity security, Reauthentication reauthentication)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/settings/account/local-login", (HttpContext http, ICurrentUser user, DisableLocalLogin handler, CancellationToken ct)
            => handler.HandleAsync(http, user.User, ct).ToHttpResultAsync())
        .RequireRateLimiting(AccountModule.CredentialsRateLimit)
        .Audited(AccountAudit.LocalLoginDisabled)
        .WithName("DisableLocalLogin");

    public async Task<Result<AccountSecurityChangeDto>> HandleAsync(HttpContext http, User user, CancellationToken ct)
    {
        var fresh = reauthentication.Require(http);
        if (!fresh.IsSuccess) return fresh.Error;
        var disabled = UserCredentials.DisableLocalLogin(user);
        if (!disabled.IsSuccess) return disabled.Error;
        if (!disabled.Value) return security.Changed(http, user);
        security.Audit(user, new { localAccount = user.LocalAccount });
        await security.NotifyAsync(user, AccountSecurity.VerifiedEmail(user), "停用了本地登入", null, ct);
        if (reauthentication.Method(http) != "local") return await security.CommitAsync(http, user, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return AccountErrors.AccountChanged; }
        await http.SignOutAsync(AuthEndpoints.CookieScheme);
        return security.Changed(http, user, signedOut: true);
    }
}
