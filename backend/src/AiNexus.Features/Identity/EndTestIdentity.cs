using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity;

/// <summary>
/// Returns from a test identity to the source administrator, only while that administrator's login, security version
/// and platform administration access are still valid. Outside a test identity it just describes the session.
/// </summary>
internal static class EndTestIdentity
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder auth) => auth
        .MapPost("/test-identity/end", (HttpContext http, NexusDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CancellationToken ct) =>
            HandleAsync(http, db, access, options.Value, csrf, ct))
        .RequireAuthorization().WithName("EndTestIdentity").Produces<AuthSessionDto>();

    private static async Task<IResult> HandleAsync(HttpContext http, NexusDbContext db, AccessService access, AdAuthenticationOptions options, IAntiforgery csrf, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId)) return Results.Ok(AuthSession.Describe(http, options, csrf));
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var method = http.User.FindFirstValue(SessionIdentity.ActorMethod);
        if (actor is null || !SessionIdentity.Allows(actor, method) || !SessionIdentity.MatchesVersion(actor, http.User.FindFirstValue(SessionIdentity.ActorVersion)) ||
            !(await access.ForUserAsync(actorId, ct)).Features.Any(x => x.Id == FeatureIds.Admin))
            return IdentityErrors.TestSourceRevoked.ToProblem();
        Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId);
        if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.TestId), out var testId)) return IdentityErrors.TestSessionInvalid.ToProblem();
        db.AuditEvents.Add(new() { OwnerId = actorId, ResourceId = testId, Action = "identity.test_end", Result = "restored", DetailsJson = JsonSerializer.Serialize(new { testId, userId = targetId, reason = "returned" }) });
        await db.SaveChangesAsync(ct);
        var previous = await http.AuthenticateAsync(AuthEndpoints.CookieScheme);
        var principal = SessionIdentity.Principal(actor, method!);
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = previous.Properties?.ExpiresUtc, AllowRefresh = true }); http.User = principal;
        return Results.Ok(AuthSession.Describe(http, options, csrf));
    }
}
