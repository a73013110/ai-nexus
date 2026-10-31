using AiNexus.Data.App;
using System.Security.Claims;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Authentication;

namespace AiNexus.Features.Admin.Identity.Sessions;

/// <summary>
/// 從測試身分切回原本的管理者；只有在該管理者的登入方式、安全版本與管理權限仍有效時才切回，否則回 <c>test_source_revoked</c>。
/// 不在測試身分中時，只回傳目前 session 的描述。
/// </summary>
internal sealed class EndTestIdentity(AppDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, AuditTrail audit)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/test-identity/end", (HttpContext http, EndTestIdentity handler, CancellationToken ct) => handler.HandleAsync(http, ct).ToHttpResultAsync())
        .Audited(IdentityAudit.TestIdentityEnded).WithName("EndTestIdentity");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId)) return AuthSession.Describe(http, options.Value, csrf);
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        if (!await SessionIdentity.ActorStillValidAsync(actor, http.User, access, ct)) return IdentityErrors.TestSourceRevoked;
        _ = Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId);
        if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.TestId), out var testId)) return IdentityErrors.TestSessionInvalid;
        audit.Describe(testId, targetId, new { testId, userId = targetId, reason = "returned" });
        await db.SaveChangesAsync(ct);
        await AuthSession.ReplaceSessionAsync(http, SessionIdentity.Principal(actor!, http.User.FindFirstValue(SessionIdentity.ActorMethod)!));
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
