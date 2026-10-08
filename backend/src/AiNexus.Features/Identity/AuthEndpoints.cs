using System.Security.Claims;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using AiNexus.Platform.Validation;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity;

public sealed record TestIdentityDto(Guid AdministratorId, string AdministratorName, Guid UserId, DateTimeOffset ExpiresAt);
public sealed record AuthSessionDto(string Mode, bool Authenticated, bool Configured, string? Account, string? DisplayName, string CsrfToken,
    IReadOnlyList<string>? Methods = null, string? Method = null, Guid? UserId = null, TestIdentityDto? Testing = null);
public sealed record AdLoginRequest(string Account, string Password, string Method = "ad");
public sealed record TestIdentityRequest(Guid UserId, string Reason);

public static class AuthEndpoints
{
    public const string CookieScheme = "NexusCookie";
    public static void MapNexusAuthentication(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithSafeErrors().WithRequestValidation();
        auth.MapGet("/session", (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) => Results.Ok(Session(http, options.Value, csrf)))
            .AllowAnonymous().WithName("GetAuthSession").Produces<AuthSessionDto>();
        auth.MapGet("/windows", async (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CurrentUser current, AuthenticationAudit audit, Issues issues, CancellationToken ct) =>
        {
            if (options.Value.Mode != "Windows") throw new ApiException(400, "authentication_mode", "此工作區未使用 Windows 整合驗證。");
            NexusUser user;
            try { user = await current.GetAsync(ct); }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                var problem = issues.Problem(error);
                await audit.WriteAsync("identity.login", null, http.User.Identity?.Name, "windows", "failed", ct, problem.Code, problem.IssueCode);
                throw;
            }
            await audit.WriteAsync("identity.login", user.Id, user.Account, "windows", "success", ct);
            return Results.Ok(Session(http, options.Value, csrf));
        }).RequireAuthorization(new AuthorizationPolicyBuilder(NegotiateDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build())
            .WithName("WindowsLogin").Produces<AuthSessionDto>();
        auth.MapPost("/login", async (AdLoginRequest body, HttpContext http, IAdAuthenticator directory, LocalAuthenticator local, CurrentUser current, NexusDbContext db, AuthenticationAudit audit, Issues issues, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CancellationToken ct) =>
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
                    http.User = new(new ClaimsIdentity([new(ClaimTypes.PrimarySid, identity.Sid), new(ClaimTypes.Name, identity.Account), new("display_name", identity.DisplayName)], CookieScheme));
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
            await EndExistingTestAsync(previous, db, "signed_in", ct);
            await audit.WriteAsync("identity.login", user.Id, user.Account, body.Method, "success", ct);
            var principal = SessionIdentity.Principal(user, body.Method);
            await http.SignInAsync(CookieScheme, principal, new AuthenticationProperties { IsPersistent = false });
            http.User = principal;
            return Results.Ok(Session(http, options.Value, csrf));
        }).AllowAnonymous().RequireRateLimiting(IdentityModule.LoginRateLimit).WithName("AdLogin").Produces<AuthSessionDto>();
        auth.MapPost("/logout", async (HttpContext http, NexusDbContext db, CurrentUser current, AuthenticationAudit audit, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, AiNexus.Features.Monitoring.RuntimeTraffic traffic, CancellationToken ct) =>
        {
            var user = await current.GetAsync(ct);
            await EndExistingTestAsync(http.User, db, "signed_out", ct);
            await audit.WriteAsync("identity.logout", user.Id, user.Account, http.User.FindFirstValue(SessionIdentity.Method) ?? "windows", "completed", ct);
            await http.SignOutAsync(CookieScheme);
            if (Guid.TryParse(http.Request.Headers["X-Nexus-Session"], out var sessionId)) traffic.Leave(user.Id, sessionId);
            http.User = new ClaimsPrincipal(new ClaimsIdentity());
            return Results.Ok(Session(http, options.Value, csrf));
        }).RequireAuthorization().WithName("AdLogout").Produces<AuthSessionDto>();
        auth.MapPost("/test-identity", async (TestIdentityRequest body, HttpContext http, CurrentUser current, NexusDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (http.User.HasClaim(x => x.Type == SessionIdentity.ActorId)) throw new ApiException(409, "test_identity_nested", "請先返回原管理者，再切換其他身分。");
            if (string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Trim().Length is < 4 or > 240) throw new ApiException(400, "test_reason_required", "請填寫 4–240 字元的測試目的。");
            var actor = await current.GetAsync(ct);
            if (!(await access.ForUserAsync(actor.Id, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature)) throw new ApiException(403, "admin_required", "需要平台管理權限。");
            var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == body.UserId && x.Enabled && x.DeletedAt == null, ct) ?? throw new ApiException(404, "user_not_found", "找不到可測試的使用者。");
            if (target.Id == actor.Id) throw new ApiException(400, "test_identity_same", "請選擇另一位使用者。");
            var expiry = DateTimeOffset.UtcNow.AddMinutes(15);
            var testId = Guid.NewGuid();
            var sourceMethod = http.User.FindFirstValue(SessionIdentity.Method) ?? (options.Value.Mode == "Windows" ? "windows" : "ad");
            db.AuditEvents.Add(new() { OwnerId = actor.Id, ResourceId = testId, Action = "identity.test_start", Result = "started", DetailsJson = JsonSerializer.Serialize(new { testId, administratorId = actor.Id, userId = target.Id, reason = body.Reason.Trim(), expiresAt = expiry }) });
            await db.SaveChangesAsync(ct);
            var previous = await http.AuthenticateAsync(CookieScheme);
            var properties = new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = previous.Properties?.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8) };
            var principal = SessionIdentity.Principal(target, "test", actor, sourceMethod, expiry, testId);
            await http.SignInAsync(CookieScheme, principal, properties); http.User = principal;
            return Results.Ok(Session(http, options.Value, csrf));
        }).RequireAuthorization(AdministrationConfiguration.Policy).WithName("StartTestIdentity").Produces<AuthSessionDto>();
        auth.MapPost("/test-identity/end", async (HttpContext http, NexusDbContext db, AccessService access, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId)) return Results.Ok(Session(http, options.Value, csrf));
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
            var method = http.User.FindFirstValue(SessionIdentity.ActorMethod);
            if (actor is null || !SessionIdentity.Allows(actor, method) || !SessionIdentity.MatchesVersion(actor, http.User.FindFirstValue(SessionIdentity.ActorVersion)) ||
                !(await access.ForUserAsync(actorId, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature))
                throw new ApiException(401, "test_source_revoked", "原管理者登入或權限已失效，請重新登入。");
            Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId);
            if (!Guid.TryParse(http.User.FindFirstValue(SessionIdentity.TestId), out var testId)) throw new ApiException(401, "test_session_invalid", "測試登入已失效。");
            db.AuditEvents.Add(new() { OwnerId = actorId, ResourceId = testId, Action = "identity.test_end", Result = "restored", DetailsJson = JsonSerializer.Serialize(new { testId, userId = targetId, reason = "returned" }) });
            await db.SaveChangesAsync(ct);
            var previous = await http.AuthenticateAsync(CookieScheme);
            var principal = SessionIdentity.Principal(actor, method!);
            await http.SignInAsync(CookieScheme, principal, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = previous.Properties?.ExpiresUtc, AllowRefresh = true }); http.User = principal;
            return Results.Ok(Session(http, options.Value, csrf));
        }).RequireAuthorization().WithName("EndTestIdentity").Produces<AuthSessionDto>();
    }
    private static async Task EndExistingTestAsync(ClaimsPrincipal previous, NexusDbContext db, string reason, CancellationToken ct)
    {
        if (!Guid.TryParse(previous.FindFirstValue(SessionIdentity.ActorId), out var actorId) ||
            !Guid.TryParse(previous.FindFirstValue(SessionIdentity.TestId), out var testId) ||
            !Guid.TryParse(previous.FindFirstValue(SessionIdentity.UserId), out var targetId)) return;
        if (await db.AuditEvents.AnyAsync(x => x.OwnerId == actorId && x.ResourceId == testId && x.Action == "identity.test_end", ct)) return;
        db.AuditEvents.Add(new() { OwnerId = actorId, ActorId = actorId, ResourceId = testId, Action = "identity.test_end", Result = "revoked", DetailsJson = JsonSerializer.Serialize(new { testId, userId = targetId, reason }) });
        await db.SaveChangesAsync(ct);
    }

    private static AuthSessionDto Session(HttpContext http, AdAuthenticationOptions options, IAntiforgery csrf)
    {
        WebSecurity.NoStore(http.Response);
        TestIdentityDto? testing = null;
        if (Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId) && Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var targetId) && long.TryParse(http.User.FindFirstValue(SessionIdentity.TestExpires), out var seconds))
            testing = new(actorId, http.User.FindFirstValue("nexus_actor_name") ?? "管理者", targetId, DateTimeOffset.FromUnixTimeSeconds(seconds));
        var methods = new List<string> { "local" };
        if (options.Mode == "Windows") methods.Insert(0, "windows"); else if (options.Configured) methods.Insert(0, "ad");
        return new(options.Mode, http.User.Identity?.IsAuthenticated == true, true, http.User.Identity?.Name, http.User.FindFirstValue("display_name"), csrf.GetAndStoreTokens(http).RequestToken!,
            methods, http.User.Identity?.IsAuthenticated == true ? http.User.FindFirstValue(SessionIdentity.Method) ?? (options.Mode == "Windows" ? "windows" : "ad") : null,
            Guid.TryParse(http.User.FindFirstValue(SessionIdentity.UserId), out var id) ? id : null, testing);
    }
}
