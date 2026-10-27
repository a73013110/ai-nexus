using AiNexus.Data.App.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>Windows 整合驗證登入：呼叫者已由 Negotiate 驗證完成，這裡只負責解析使用者並寫入稽核。</summary>
internal sealed class WindowsLogin(IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CurrentUser current, AuthenticationAudit audit, Issues issues)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapGet("/windows", (HttpContext http, WindowsLogin handler, CancellationToken ct) => handler.HandleAsync(http, ct))
        .RequireAuthorization(new AuthorizationPolicyBuilder(NegotiateDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build())
        .NotAudited("登入結果由登入稽核記錄")
        .WithName("WindowsLogin");

    public async Task<Results<Ok<AuthSessionDto>, ProblemHttpResult>> HandleAsync(HttpContext http, CancellationToken ct)
    {
        if (options.Value.Mode != "Windows") return IdentityErrors.AuthenticationModeUnsupported.ToProblem();
        Result<User> user;
        try { user = await current.GetAsync(ct); }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            var problem = issues.Problem(error);
            await audit.WriteAsync(IdentityAudit.SignedIn, null, http.User.Identity?.Name, "windows", ct, problem.Code, problem.IssueCode);
            throw;
        }
        if (!user.IsSuccess)
        {
            var issue = issues.Report(user.Error);
            await audit.WriteAsync(IdentityAudit.SignedIn, null, http.User.Identity?.Name, "windows", ct, user.Error.Code, issue);
            return user.Error.ToProblem(issue);
        }
        await audit.WriteAsync(IdentityAudit.SignedIn, user.Value.Id, UserAccounts.LoginName(user.Value), "windows", ct);
        return TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
    }
}
