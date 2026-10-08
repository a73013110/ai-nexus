using System.Security.Claims;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity;

/// <summary>Ends any test identity, audits the sign-out, clears the cookie and leaves the live-traffic session.</summary>
internal static class LogOut
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder auth) => auth
        .MapPost("/logout", async (HttpContext http, NexusDbContext db, CurrentUser current, AuthenticationAudit audit, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, RuntimeTraffic traffic, CancellationToken ct) =>
        {
            var user = await current.GetAsync(ct);
            await AuthSession.EndExistingTestAsync(http.User, db, "signed_out", ct);
            await audit.WriteAsync("identity.logout", user.Id, user.Account, http.User.FindFirstValue(SessionIdentity.Method) ?? "windows", "completed", ct);
            await http.SignOutAsync(AuthEndpoints.CookieScheme);
            if (Guid.TryParse(http.Request.Headers["X-Nexus-Session"], out var sessionId)) traffic.Leave(user.Id, sessionId);
            http.User = new ClaimsPrincipal(new ClaimsIdentity());
            return Results.Ok(AuthSession.Describe(http, options.Value, csrf));
        }).RequireAuthorization().WithName("AdLogout").Produces<AuthSessionDto>();
}
