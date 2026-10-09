using System.Security.Claims;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity;

public static class SessionIdentity
{
    public const string UserId = "nexus_user_id";
    public const string Version = "nexus_security_version";
    public const string Method = "nexus_method";
    public const string ActorId = "nexus_actor_id";
    public const string ActorVersion = "nexus_actor_version";
    public const string ActorMethod = "nexus_actor_method";
    public const string TestExpires = "nexus_test_expires";
    public const string TestId = "nexus_test_id";
    public const string Restored = "Nexus.IdentityRestored";
    private const string Validated = "Nexus.ValidatedUser";

    /// <summary>The untracked user row this request's cookie was validated against, when it is still the signed-in user.</summary>
    internal static NexusUser? ValidatedUser(HttpContext http, Guid id) => http.Items[Validated] is NexusUser user && user.Id == id ? user : null;

    public static ClaimsPrincipal Principal(NexusUser user, string method, NexusUser? actor = null, string? actorMethod = null, DateTimeOffset? testExpires = null, Guid? testId = null)
    {
        var claims = new List<Claim>
        {
            new(UserId, user.Id.ToString()), new(Version, user.SecurityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(Method, method), new(ClaimTypes.PrimarySid, user.Sid), new(ClaimTypes.Name, user.Account), new("display_name", user.DisplayName)
        };
        if (actor is not null)
        {
            claims.Add(new(ActorId, actor.Id.ToString())); claims.Add(new(ActorVersion, actor.SecurityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            claims.Add(new(ActorMethod, actorMethod!)); claims.Add(new(TestExpires, testExpires!.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            claims.Add(new("nexus_actor_name", actor.DisplayName));
            claims.Add(new(TestId, (testId ?? Guid.NewGuid()).ToString()));
        }
        return new(new ClaimsIdentity(claims, AuthEndpoints.CookieScheme));
    }

    public static bool Available(NexusUser user) => user.Enabled && user.DeletedAt is null;
    public static bool Allows(NexusUser user, string? method) => Available(user) && (method switch
    {
        "local" => user.LocalEnabled && user.PasswordHash is not null,
        "ad" or "windows" => user.AdEnabled,
        _ => false
    });
    public static bool MatchesVersion(NexusUser user, string? version) => int.TryParse(version, out var value) && value == user.SecurityVersion;

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<NexusDbContext>();
        var principal = context.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(UserId), out var id))
        {
            // Cookies from the previous AD-only release remain usable until a policy change.
            var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
            var legacy = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, context.HttpContext.RequestAborted);
            if (legacy is null || Allows(legacy, "ad") && legacy.SecurityVersion == 0) return;
            context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return;
        }
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
        if (Guid.TryParse(principal.FindFirstValue(ActorId), out var actorId))
        {
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, context.HttpContext.RequestAborted);
            var access = context.HttpContext.RequestServices.GetRequiredService<AccessService>();
            if (actor is null || !Allows(actor, principal.FindFirstValue(ActorMethod)) || !MatchesVersion(actor, principal.FindFirstValue(ActorVersion)) ||
                !(await access.ForUserAsync(actorId, context.HttpContext.RequestAborted)).Features.Any(x => x.Id == FeatureIds.Admin))
            { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return; }
            if (!Guid.TryParse(principal.FindFirstValue(TestId), out var testId))
            { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return; }
            // Reuse the indexed audit resource key for session revocation, without parsing/scanning JSON.
            var ended = await db.AuditEvents.AsNoTracking().AnyAsync(x => x.ResourceId == testId && x.OwnerId == actorId && x.Action == "identity.test_end", context.HttpContext.RequestAborted);
            var expired = !long.TryParse(principal.FindFirstValue(TestExpires), out var seconds) || seconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (ended || expired || user is null || !Available(user) || !MatchesVersion(user, principal.FindFirstValue(Version)))
            {
                // Restore only the still-authorized source administrator, never extend the test.
                context.ReplacePrincipal(Principal(actor, principal.FindFirstValue(ActorMethod)!)); context.ShouldRenew = true;
                context.HttpContext.Items[Restored] = true;
                context.Properties.AllowRefresh = true;
                if (!ended)
                {
                    db.AuditEvents.Add(new() { OwnerId = actorId, ResourceId = testId, Action = "identity.test_end", Result = "restored", DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { testId, userId = id, reason = expired ? "expired" : "target_changed" }) });
                    await db.SaveChangesAsync(context.HttpContext.RequestAborted);
                }
                return;
            }
            context.HttpContext.Items[Validated] = user;
            return;
        }
        if (user is null || !Allows(user, principal.FindFirstValue(Method)) || !MatchesVersion(user, principal.FindFirstValue(Version)))
        { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return; }
        context.HttpContext.Items[Validated] = user;
    }
}
