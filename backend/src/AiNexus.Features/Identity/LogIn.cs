using System.Security.Claims;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity;

/// <summary>
/// Deliberately has no request validator: an empty or oversized account or password answers 401
/// <c>invalid_credentials</c> and is audited exactly like wrong credentials, so the two cannot be told apart.
/// </summary>
public sealed record AdLoginRequest(string Account, string Password, string Method = "ad");

/// <summary>
/// Local or LDAP password sign-in. Failures stay exceptions so the single catch audits each one with its public code
/// and issue code before the diagnostic middleware answers.
/// </summary>
internal static class LogIn
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder auth) => auth
        .MapPost("/login", async (AdLoginRequest body, HttpContext http, IAdAuthenticator directory, LocalAuthenticator local, CurrentUser current, NexusDbContext db, AuthenticationAudit audit, Issues issues, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CancellationToken ct) =>
        {
            var previous = http.User;
            NexusUser user;
            try
            {
                if (string.IsNullOrWhiteSpace(body.Account) || body.Account.Length > 256 || body.Password is null || body.Password.Length is < 1 or > 1024)
                    throw new ApiException(401, "invalid_credentials", "帳號、密碼或登入方式不正確，或帳號暫時無法登入。");
                if (body.Method == "local") user = await local.AuthenticateAsync(body.Account, body.Password, ct);
                else if (body.Method == "ad" && options.Value.Mode == "Ldap")
                {
                    var identity = await directory.AuthenticateAsync(body.Account.Trim(), body.Password, ct);
                    http.User = new(new ClaimsIdentity([new(ClaimTypes.PrimarySid, identity.Sid), new(ClaimTypes.Name, identity.Account), new("display_name", identity.DisplayName)], AuthEndpoints.CookieScheme));
                    user = await current.GetAsync(ct);
                }
                else throw new ApiException(400, "authentication_mode", "請選擇此部署支援的登入方式。");
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                http.User = previous;
                var problem = issues.Problem(error);
                await audit.WriteAsync("identity.login", null, body.Account, body.Method, "failed", ct, problem.Code, problem.IssueCode);
                throw;
            }
            await AuthSession.EndExistingTestAsync(previous, db, "signed_in", ct);
            await audit.WriteAsync("identity.login", user.Id, user.Account, body.Method, "success", ct);
            var principal = SessionIdentity.Principal(user, body.Method);
            await http.SignInAsync(AuthEndpoints.CookieScheme, principal, new AuthenticationProperties { IsPersistent = false });
            http.User = principal;
            return Results.Ok(AuthSession.Describe(http, options.Value, csrf));
        }).AllowAnonymous().RequireRateLimiting(IdentityModule.LoginRateLimit).WithName("AdLogin").Produces<AuthSessionDto>();
}
