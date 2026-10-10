using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

public sealed record TestIdentityRequest(Guid UserId, string Reason);

/// <summary>
/// The test purpose. A session that is already a test identity is refused first, with its own code, by the handler,
/// so this rule only applies outside one.
/// </summary>
internal sealed class TestIdentityRequestValidator : RequestValidator<TestIdentityRequest>
{
    public override string ProblemCode => IdentityErrors.TestReasonRequired.Code;

    public TestIdentityRequestValidator(IHttpContextAccessor accessor)
    {
        When(_ => !StartTestIdentity.IsTestIdentity(accessor.HttpContext?.User), () =>
            RuleFor(x => x.Reason).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length is >= StartTestIdentity.MinReasonLength and <= StartTestIdentity.MaxReasonLength)
                .WithErrorCode("length"));
    }
}

/// <summary>
/// Lets a platform administrator act as another enabled user for 15 minutes. The audit row is written before the cookie
/// changes; the original administrator's cookie lifetime is kept.
/// </summary>
internal sealed class StartTestIdentity(CurrentUser current, NexusDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, TimeProvider clock)
{
    public const int MinReasonLength = 4, MaxReasonLength = 240;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/test-identity", (TestIdentityRequest body, HttpContext http, StartTestIdentity handler, CancellationToken ct) => handler.HandleAsync(http, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.Admin).WithName("StartTestIdentity");

    public static bool IsTestIdentity(ClaimsPrincipal? user) => user?.HasClaim(x => x.Type == SessionIdentity.ActorId) == true;

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, TestIdentityRequest body, CancellationToken ct)
    {
        if (IsTestIdentity(http.User)) return IdentityErrors.TestIdentityNested;
        var resolved = await current.GetAsync(ct);
        if (!resolved.IsSuccess) return resolved.Error;
        var actor = resolved.Value;
        if (!(await access.ForUserAsync(actor.Id, ct)).Features.Any(x => x.Id == FeatureIds.Admin)) return IdentityErrors.AdminRequired;
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == body.UserId && x.Enabled && x.DeletedAt == null, ct);
        if (target is null) return IdentityErrors.TestUserNotFound;
        if (target.Id == actor.Id) return IdentityErrors.TestIdentitySame;
        var now = clock.GetUtcNow();
        var expiry = now.Add(Lifetime);
        var testId = Guid.NewGuid();
        var sourceMethod = http.User.FindFirstValue(SessionIdentity.Method) ?? (options.Value.Mode == "Windows" ? "windows" : "ad");
        db.AuditEvents.Add(new() { OwnerId = actor.Id, ResourceId = testId, Action = "identity.test_start", Result = "started", DetailsJson = JsonSerializer.Serialize(new { testId, administratorId = actor.Id, userId = target.Id, reason = body.Reason.Trim(), expiresAt = expiry }) });
        await db.SaveChangesAsync(ct);
        var previous = await http.AuthenticateAsync(AuthEndpoints.CookieScheme);
        var properties = new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = previous.Properties?.ExpiresUtc ?? now.AddHours(8) };
        var principal = SessionIdentity.Principal(target, "test", actor, sourceMethod, expiry, testId);
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, properties); http.User = principal;
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
