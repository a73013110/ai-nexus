using System.Security.Claims;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Sessions;

[ValidatedInHandler("空白或超長的密碼與密碼錯誤走同一個回應與稽核，不透露輸入格式")]
public sealed record UnlockSessionRequest(string Password);

/// <summary>
/// 以目前登入方式的密碼解開鎖定，並重新起算所有期限。principal 不變，所以 CSRF token 繼續有效，暫存的請求可以直接重送。
/// 猜密碼的防護與登入共用失敗計數（<see cref="Reauthentication.VerifyPasswordAsync"/>）。
/// </summary>
internal sealed class UnlockSession(CurrentUser current, Reauthentication reauthentication, SessionTicket ticket, AuthenticationAudit audit, Issues issues,
    IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder auth) => auth
        .MapPost("/unlock", (UnlockSessionRequest body, HttpContext http, UnlockSession handler, CancellationToken ct) => handler.HandleAsync(http, body, ct))
        .AllowWhileLocked().RequireRateLimiting(IdentityModule.LoginRateLimit).NotAudited("解鎖結果由登入稽核記錄").WithName("UnlockSession");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, UnlockSessionRequest body, CancellationToken ct)
    {
        // 測試身分不會鎖定；放行的話，輸錯的次數會算到被測者的帳號上。
        var allowed = Reauthentication.Allowed(http);
        if (!allowed.IsSuccess) return allowed.Error;
        var resolved = await current.GetAsync(ct);
        if (!resolved.IsSuccess) return resolved.Error;
        var user = resolved.Value;
        var method = http.User.FindFirstValue(SessionIdentity.Method);
        var verified = await reauthentication.VerifyPasswordAsync(http, user, body.Password, ct);
        if (!verified.IsSuccess)
        {
            await audit.WriteAsync(IdentityAudit.SessionUnlocked, user.Id, UserAccounts.LoginName(user), method, ct, verified.Error.Code, issues.Report(verified.Error));
            return verified.Error;
        }
        await audit.WriteAsync(IdentityAudit.SessionUnlocked, user.Id, UserAccounts.LoginName(user), method, ct);
        await ticket.IssueAsync(http, http.User);
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
