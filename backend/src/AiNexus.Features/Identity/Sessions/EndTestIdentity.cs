using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

/// <summary>
/// Returns from a test identity to the source administrator, only while that administrator's login, security version
/// and platform administration access are still valid. Outside a test identity it just describes the session.
/// </summary>
internal sealed class EndTestIdentity(NexusDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/test-identity/end", (HttpContext http, EndTestIdentity handler, CancellationToken ct) => handler.HandleAsync(http, ct).ToHttpResultAsync())
        .RequireAuthorization().WithName("EndTestIdentity");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId)) return AuthSession.Describe(http, options.Value, csrf);
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var method = http.User.FindFirstValue(SessionIdentity.ActorMethod);
        if (actor is null || !SessionIdentity.Allows(actor, method) || !SessionIdentity.MatchesVersion(actor, http.User.FindFirstValue(SessionIdentity.ActorVersion)) ||
            !(await access.ForUserAsync(actorId, ct)).Features.Any(x => x.Id == FeatureIds.Admin))
            return IdentityErrors.TestSourceRevoked;
        _ = Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId);
        if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.TestId), out var testId)) return IdentityErrors.TestSessionInvalid;
        db.AuditEvents.Add(new() { OwnerId = actorId, ResourceId = testId, Action = "identity.test_end", Result = "restored", DetailsJson = JsonSerializer.Serialize(new { testId, userId = targetId, reason = "returned" }) });
        await db.SaveChangesAsync(ct);
        var previous = await http.AuthenticateAsync(AuthEndpoints.CookieScheme);
        var principal = SessionIdentity.Principal(actor, method!);
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = previous.Properties?.ExpiresUtc, AllowRefresh = true }); http.User = principal;
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
