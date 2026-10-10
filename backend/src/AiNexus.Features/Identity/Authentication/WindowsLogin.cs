using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>Windows integrated sign-in: Negotiate has already authenticated the caller; this resolves and audits the user.</summary>
internal sealed class WindowsLogin(IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CurrentUser current, AuthenticationAudit audit, Issues issues)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapGet("/windows", (HttpContext http, WindowsLogin handler, CancellationToken ct) => handler.HandleAsync(http, ct))
        .RequireAuthorization(new AuthorizationPolicyBuilder(NegotiateDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build())
        .WithName("WindowsLogin");

    public async Task<Results<Ok<AuthSessionDto>, ProblemHttpResult>> HandleAsync(HttpContext http, CancellationToken ct)
    {
        if (options.Value.Mode != "Windows") return IdentityErrors.AuthenticationModeUnsupported.ToProblem();
        Result<NexusUser> user;
        try { user = await current.GetAsync(ct); }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            var problem = issues.Problem(error);
            await audit.WriteAsync("identity.login", null, http.User.Identity?.Name, "windows", "failed", ct, problem.Code, problem.IssueCode);
            throw;
        }
        if (!user.IsSuccess)
        {
            var issue = issues.Report(user.Error);
            await audit.WriteAsync("identity.login", null, http.User.Identity?.Name, "windows", "failed", ct, user.Error.Code, issue);
            return user.Error.ToProblem(issue);
        }
        await audit.WriteAsync("identity.login", user.Value.Id, user.Value.Account, "windows", "success", ct);
        return TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
    }
}
