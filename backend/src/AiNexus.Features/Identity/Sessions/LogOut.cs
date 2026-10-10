using System.Security.Claims;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

/// <summary>Ends any test identity, audits the sign-out, clears the cookie and announces the ended browser session.</summary>
internal sealed class LogOut(NexusDbContext db, CurrentUser current, AuthenticationAudit audit, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, DomainEvents events)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/logout", (HttpContext http, LogOut handler, CancellationToken ct) => handler.HandleAsync(http, ct).ToHttpResultAsync())
        .RequireAuthorization().WithName("AdLogout");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        if (!user.IsSuccess) return user.Error;
        await AuthSession.EndExistingTestAsync(http.User, db, "signed_out", ct);
        await audit.WriteAsync("identity.logout", user.Value.Id, user.Value.Account, http.User.FindFirstValue(SessionIdentity.Method) ?? "windows", "completed", ct);
        await http.SignOutAsync(AuthEndpoints.CookieScheme);
        if (Guid.TryParse(http.Request.Headers["X-Nexus-Session"], out var sessionId))
        {
            events.Raise(new UserSignedOut(user.Value.Id, sessionId));
            await db.SaveChangesAsync(ct);
        }
        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
