using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>Windows integrated sign-in: Negotiate has already authenticated the caller; this resolves and audits the user.</summary>
internal static class WindowsLogin
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder auth) => auth
        .MapGet("/windows", async (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CurrentUser current, AuthenticationAudit audit, Issues issues, CancellationToken ct) =>
        {
            if (options.Value.Mode != "Windows") return IdentityErrors.WindowsModeRequired.ToProblem();
            NexusUser user;
            try { user = await current.GetAsync(ct); }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                var problem = issues.Problem(error);
                await audit.WriteAsync("identity.login", null, http.User.Identity?.Name, "windows", "failed", ct, problem.Code, problem.IssueCode);
                throw;
            }
            await audit.WriteAsync("identity.login", user.Id, user.Account, "windows", "success", ct);
            return Results.Ok(AuthSession.Describe(http, options.Value, csrf));
        }).RequireAuthorization(new AuthorizationPolicyBuilder(NegotiateDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build())
        .WithName("WindowsLogin").Produces<AuthSessionDto>();
}
