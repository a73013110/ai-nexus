using AiNexus.Data.App;
using System.Security.Claims;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.Features.Admin.Identity.Sessions;

[ValidatedInHandler("已在測試身分時要先以 test_identity_nested 拒絕，測試目的的長度才有意義")]
public sealed record TestIdentityRequest(Guid UserId, string Reason);

/// <summary>
/// 讓平台管理員以另一位已啟用使用者的身分操作，為期 15 分鐘。稽核紀錄先於 cookie 變更寫入；原管理者 cookie 的存續時間保持不變。
/// </summary>
internal sealed class StartTestIdentity(CurrentUser current, AppDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, TimeProvider clock, AuditTrail audit)
{
    public const int MinReasonLength = 4, MaxReasonLength = 240;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/test-identity", (TestIdentityRequest body, HttpContext http, StartTestIdentity handler, CancellationToken ct)
            => handler.HandleAsync(http, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.IdentityUsers).Audited(IdentityAudit.TestIdentityStarted).WithName("StartTestIdentity");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, TestIdentityRequest body, CancellationToken ct)
    {
        if (SessionIdentity.IsTest(http.User)) return IdentityErrors.TestIdentityNested;
        // JSON 可能送 null；只有空白的理由修剪後長度為 0，同樣拒絕。
        if (body.Reason?.Trim() is not { Length: >= MinReasonLength and <= MaxReasonLength } reason) return IdentityErrors.TestReasonRequired;
        var resolved = await current.GetAsync(ct);
        if (!resolved.IsSuccess) return resolved.Error;
        var actor = resolved.Value;
        if (!await access.HoldsAsync(actor.Id, FeatureCatalog.IdentityUsers, ct)) return AccessControlErrors.FeatureForbidden;
        var target = await db.Users.AsNoTracking().Where(ActiveUsers.Active).SingleOrDefaultAsync(x => x.Id == body.UserId, ct);
        if (target is null) return IdentityErrors.UserNotFound;
        if (target.Id == actor.Id) return IdentityErrors.TestIdentitySame;
        var now = clock.GetUtcNow();
        var expiry = now.Add(Lifetime);
        var testId = Guid.NewGuid();
        var sourceMethod = http.User.FindFirstValue(SessionIdentity.Method) ?? options.Value.DirectoryMethod;
        audit.Describe(testId, target.Id, new { testId, administratorId = actor.Id, userId = target.Id, reason, expiresAt = expiry });
        await db.SaveChangesAsync(ct);
        var previous = await http.AuthenticateAsync(AuthEndpoints.CookieScheme);
        var properties = new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = previous.Properties?.ExpiresUtc ?? now.AddHours(8) };
        var principal = SessionIdentity.Principal(target, "test", actor, sourceMethod, expiry, testId);
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, properties); http.User = principal;
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
