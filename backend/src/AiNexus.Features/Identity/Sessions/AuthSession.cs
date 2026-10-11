using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

public sealed record TestIdentityDto(Guid AdministratorId, string AdministratorName, Guid UserId, DateTimeOffset ExpiresAt);

/// <summary>An identity the signed-in account can act as; <c>Kind</c> is <c>personal</c> today.</summary>
public sealed record SignInIdentityDto(string Id, string Kind, string Name, string? Detail);

public sealed record AuthSessionDto(string Mode, bool Authenticated, bool Configured, string? Account, string? DisplayName, string CsrfToken,
    IReadOnlyList<string>? Methods = null, string? Method = null, Guid? UserId = null, TestIdentityDto? Testing = null,
    IReadOnlyList<SignInIdentityDto>? Identities = null, string? Identity = null);

/// <summary>The session description every auth endpoint answers with, and the test-identity bookkeeping they share.</summary>
internal static class AuthSession
{
    public const string PersonalIdentity = "personal";

    /// <summary>
    /// The identities a signed-in account may choose between right after sign-in. Every account has only its personal
    /// identity today, so the login page skips the choice. A future source (organisations, delegations) adds its options
    /// here and must also decide what access each one carries and audit the choice.
    /// </summary>
    public static IReadOnlyList<SignInIdentityDto> Identities(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? [new(PersonalIdentity, PersonalIdentity, user.FindFirstValue("display_name") ?? user.Identity.Name ?? "", user.Identity.Name)]
        : [];

    public static AuthSessionDto Describe(HttpContext http, AdAuthenticationOptions options, IAntiforgery csrf)
    {
        WebSecurity.NoStore(http.Response);
        TestIdentityDto? testing = null;
        if (Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId) && Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId) && long.TryParse(http.User.FindFirstValue(SessionIdentity.TestExpires), out var seconds))
            testing = new(actorId, http.User.FindFirstValue("nexus_actor_name") ?? "管理者", targetId, DateTimeOffset.FromUnixTimeSeconds(seconds));
        var methods = new List<string> { "local" };
        if (options.Mode == "Windows") methods.Insert(0, "windows"); else if (options.Configured) methods.Insert(0, "ad");
        var identities = Identities(http.User);
        var chosen = http.User.FindFirstValue(SessionIdentity.ActiveIdentity);
        return new(options.Mode, http.User.Identity?.IsAuthenticated == true, true, http.User.Identity?.Name, http.User.FindFirstValue("display_name"), csrf.GetAndStoreTokens(http).RequestToken!,
            methods, http.User.Identity?.IsAuthenticated == true ? http.User.FindFirstValue(SessionIdentity.Method) ?? (options.Mode == "Windows" ? "windows" : "ad") : null,
            Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var id) ? id : null, testing,
            identities, identities.FirstOrDefault(x => x.Id == chosen)?.Id ?? (identities.Count > 0 ? identities[0].Id : null));
    }

    /// <summary>Records that a test identity carried by <paramref name="previous"/> ended, unless it already has.</summary>
    public static async Task EndExistingTestAsync(ClaimsPrincipal previous, NexusDbContext db, string reason, CancellationToken ct)
    {
        if (!Guid.TryParse(previous.FindFirstValue(SessionIdentity.ActorId), out var actorId) ||
            !Guid.TryParse(previous.FindFirstValue(SessionIdentity.TestId), out var testId) ||
            !Guid.TryParse(previous.FindFirstValue(SessionIdentity.UserId), out var targetId)) return;
        if (await db.AuditEvents.AnyAsync(x => x.OwnerId == actorId && x.ResourceId == testId && x.Action == "identity.test_end", ct)) return;
        db.AuditEvents.Add(new() { OwnerId = actorId, ActorId = actorId, ResourceId = testId, Action = "identity.test_end", Result = "revoked", DetailsJson = JsonSerializer.Serialize(new { testId, userId = targetId, reason }) });
        await db.SaveChangesAsync(ct);
    }
}
