using AiNexus.Platform.Validation;
using System.Security.Claims;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity.Authentication;

[ValidatedInHandler("An empty or oversized account or password answers 401 invalid_credentials and is audited like wrong credentials, so the two cannot be told apart.")]
public sealed record AdLoginRequest(string Account, string Password, string Method = "ad");

/// <summary>Local or LDAP password sign-in. Every failure is audited with the issue code its response carries.</summary>
internal sealed class LogIn(IAdAuthenticator directory, LocalAuthenticator local, CurrentUser current, NexusDbContext db, AuthenticationAudit audit, Issues issues,
    IOptions<AdAuthenticationOptions> options, IAntiforgery csrf)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/login", (AdLoginRequest body, HttpContext http, LogIn handler, CancellationToken ct) => handler.HandleAsync(http, body, ct))
        .AllowAnonymous().RequireRateLimiting(IdentityModule.LoginRateLimit).WithName("AdLogin");

    public async Task<Results<Ok<AuthSessionDto>, ProblemHttpResult>> HandleAsync(HttpContext http, AdLoginRequest body, CancellationToken ct)
    {
        var previous = http.User;
        Result<NexusUser> user;
        try { user = await AuthenticateAsync(http, body, ct); }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            http.User = previous;
            var problem = issues.Problem(error);
            await audit.WriteAsync("identity.login", null, body.Account, body.Method, "failed", ct, problem.Code, problem.IssueCode);
            throw;
        }
        if (!user.IsSuccess)
        {
            http.User = previous;
            var issue = issues.Report(user.Error);
            await audit.WriteAsync("identity.login", null, body.Account, body.Method, "failed", ct, user.Error.Code, issue);
            return user.Error.ToProblem(issue);
        }
        await AuthSession.EndExistingTestAsync(previous, db, "signed_in", ct);
        await audit.WriteAsync("identity.login", user.Value.Id, user.Value.Account, body.Method, "success", ct);
        var principal = SessionIdentity.Principal(user.Value, body.Method);
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, new AuthenticationProperties { IsPersistent = false });
        http.User = principal;
        return TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
    }

    private async Task<Result<NexusUser>> AuthenticateAsync(HttpContext http, AdLoginRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Account) || body.Account.Length > 256 || body.Password is null || body.Password.Length is < 1 or > 1024)
            return IdentityErrors.InvalidCredentials;
        if (body.Method == "local") return await local.AuthenticateAsync(body.Account, body.Password, ct);
        if (body.Method != "ad" || options.Value.Mode != "Ldap") return IdentityErrors.AuthenticationModeUnsupported;
        var identity = await directory.AuthenticateAsync(body.Account.Trim(), body.Password, ct);
        if (!identity.IsSuccess) return identity.Error;
        http.User = new(new ClaimsIdentity([new(ClaimTypes.PrimarySid, identity.Value.Sid), new(ClaimTypes.Name, identity.Value.Account), new("display_name", identity.Value.DisplayName)], AuthEndpoints.CookieScheme));
        return await current.GetAsync(ct);
    }
}
