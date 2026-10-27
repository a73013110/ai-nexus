using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Validation;
using System.Globalization;
using System.Security.Claims;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.Features.Admin.Identity.Authentication;

[ValidatedInHandler("An empty or oversized account or password answers 401 invalid_credentials and is audited like wrong credentials, so the two cannot be told apart.")]
public sealed record AdLoginRequest(string Account, string Password, string Method = "ad");

/// <summary>本地或 LDAP 帳密登入。每次失敗都會以回應中帶的 issue code 寫入稽核。</summary>
internal sealed class LogIn(IAdAuthenticator directory, LocalAuthenticator local, CurrentUser current, AppDbContext db, AuthenticationAudit audit, Issues issues,
    IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, AuditTrail trail, LoginThrottle throttle)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/login", (AdLoginRequest body, HttpContext http, LogIn handler, CancellationToken ct) => handler.HandleAsync(http, body, ct))
        .AllowAnonymous().RequireRateLimiting(IdentityModule.LoginRateLimit).NotAudited("登入結果由登入稽核記錄").WithName("AdLogin");

    public async Task<Results<Ok<AuthSessionDto>, ProblemHttpResult>> HandleAsync(HttpContext http, AdLoginRequest body, CancellationToken ct)
    {
        var previous = http.User;
        var account = LoginThrottle.Key(body, options.Value);
        Result<User> user;
        // 達上限時連目錄都不查：猜密碼的請求不該再打到 AD，也不會讓 AD 因此鎖住帳號。
        if (throttle.RetryAfter(account) is { } wait)
        {
            http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            user = IdentityErrors.LoginRateLimited;
        }
        else
        {
            try { user = await AuthenticateAsync(http, body, ct); }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                http.User = previous;
                var problem = issues.Problem(error);
                await audit.WriteAsync(IdentityAudit.SignedIn, null, body.Account, body.Method, ct, problem.Code, problem.IssueCode);
                throw;
            }
        }
        if (!user.IsSuccess)
        {
            http.User = previous;
            // 只有帳密類的失敗才計數；目錄連不上等上游錯誤不是猜密碼，不該把使用者鎖在外面。
            if (user.Error.Kind == ErrorKind.Unauthenticated) throttle.Failed(account);
            var issue = issues.Report(user.Error);
            await audit.WriteAsync(IdentityAudit.SignedIn, null, body.Account, body.Method, ct, user.Error.Code, issue);
            return user.Error.ToProblem(issue);
        }
        await AuthSession.EndExistingTestAsync(previous, db, trail, "signed_in", ct);
        await audit.WriteAsync(IdentityAudit.SignedIn, user.Value.Id, UserAccounts.LoginName(user.Value), body.Method, ct);
        var principal = SessionIdentity.Principal(user.Value, body.Method);
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, new AuthenticationProperties { IsPersistent = false });
        http.User = principal;
        return TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
    }

    private async Task<Result<User>> AuthenticateAsync(HttpContext http, AdLoginRequest body, CancellationToken ct)
    {
        // 空白或超長的帳密與帳號密碼錯誤走同一個回應與稽核，避免從回應差異推測輸入格式。
        if (string.IsNullOrWhiteSpace(body.Account) || body.Account.Length > 256
            || body.Password is null || body.Password.Length is < 1 or > 1024)
            return IdentityErrors.InvalidCredentials;
        if (body.Method == "local") return await local.AuthenticateAsync(body.Account, body.Password, ct);
        if (body.Method != "ad" || options.Value.Mode != "Ldap") return IdentityErrors.AuthenticationModeUnsupported;
        var settings = options.Value;
        var identity = await directory.AuthenticateAsync(UserAccounts.DirectoryLoginName(body.Account, settings.Domain, settings.AccountSuffixes), body.Password, ct);
        if (!identity.IsSuccess) return identity.Error;
        var claims = new ClaimsIdentity(
        [
            new(ClaimTypes.PrimarySid, identity.Value.Sid),
            new(ClaimTypes.Name, identity.Value.Account),
            new(SessionIdentity.DisplayName, identity.Value.DisplayName),
        ], AuthEndpoints.CookieScheme);
        // 只給 CurrentUser 同步信箱用；登入成功後 cookie 以 SessionIdentity.Principal 重建，不帶這個 claim。
        if (identity.Value.Email is { } email) claims.AddClaim(new(SessionIdentity.DirectoryEmail, email));
        http.User = new(claims);
        return await current.GetAsync(ct);
    }
}
