using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using System.Security.Claims;
using AiNexus.Features.AccessControl;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity.Sessions;

public static class SessionIdentity
{
    public const string UserId = "user_id";
    public const string Version = "security_version";
    public const string Method = "auth_method";
    public const string ActorId = RequestClaims.ActorId;
    public const string ActorVersion = "actor_version";
    public const string ActorMethod = "actor_method";
    public const string TestExpires = "test_expires";
    public const string TestId = "test_id";
    public const string ActiveIdentity = "active_identity";
    public const string Restored = "Identity.Restored";
    private const string Validated = "Identity.ValidatedUser";

    /// <summary>此請求的 cookie 驗證時讀到的使用者列（未追蹤）；只有該使用者仍是目前登入者時才回傳。</summary>
    internal static User? ValidatedUser(HttpContext http, Guid id) => http.Items[Validated] is User user && user.Id == id ? user : null;

    public static ClaimsPrincipal Principal(User user, string method, User? actor = null, string? actorMethod = null, DateTimeOffset? testExpires = null, Guid? testId = null)
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
            claims.Add(new("actor_name", actor.DisplayName));
            claims.Add(new(TestId, (testId ?? Guid.NewGuid()).ToString()));
        }
        return new(new ClaimsIdentity(claims, AuthEndpoints.CookieScheme));
    }

    public static bool Available(User user) => user.Enabled && user.DeletedAt is null;
    public static bool Allows(User user, string? method) => Available(user) && (method switch
    {
        "local" => user.LocalEnabled && user.PasswordHash is not null,
        "ad" or "windows" => user.AdEnabled,
        _ => false
    });
    public static bool MatchesVersion(User user, string? version) => int.TryParse(version, out var value) && value == user.SecurityVersion;

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var principal = context.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(UserId), out var id))
        {
            // 舊版（僅 AD 登入）發出的 cookie 沒有 UserId，改以 SID 對應；只有仍可 AD 登入且安全版本為 0 的使用者才繼續有效。
            var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
            var legacy = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, context.HttpContext.RequestAborted);
            if (legacy is null || (Allows(legacy, "ad") && legacy.SecurityVersion == 0)) return;
            context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return;
        }
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
        if (Guid.TryParse(principal.FindFirstValue(ActorId), out var actorId))
        {
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, context.HttpContext.RequestAborted);
            var access = context.HttpContext.RequestServices.GetRequiredService<AccessService>();
            if (actor is null || !Allows(actor, principal.FindFirstValue(ActorMethod)) || !MatchesVersion(actor, principal.FindFirstValue(ActorVersion))
                || !(await access.ForUserAsync(actorId, context.HttpContext.RequestAborted)).Features.Any(x => x.Id == FeatureIds.Admin))
            { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return; }
            if (!Guid.TryParse(principal.FindFirstValue(TestId), out var testId))
            { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme); return; }
            // 直接用已建索引的 ResourceId 欄位查稽核紀錄判斷測試身分是否已結束，不解析 DetailsJson。
            var ended = await db.AuditEvents.AsNoTracking().AnyAsync(x => x.ResourceId == testId && x.OwnerId == actorId && x.Action == "identity.test_end", context.HttpContext.RequestAborted);
            var expired = !long.TryParse(principal.FindFirstValue(TestExpires), out var seconds) || seconds <= context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds();
            if (ended || expired || user is null || !Available(user) || !MatchesVersion(user, principal.FindFirstValue(Version)))
            {
                // 只切回仍有權限的原管理者，且絕不延長測試身分的有效期。
                context.ReplacePrincipal(Principal(actor, principal.FindFirstValue(ActorMethod)!)); context.ShouldRenew = true;
                context.HttpContext.Items[Restored] = true;
                context.Properties.AllowRefresh = true;
                if (!ended)
                {
                    db.AuditEvents.Add(new()
                    {
                        OwnerId = actorId,
                        ResourceId = testId,
                        Action = "identity.test_end",
                        Result = "restored",
                        DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { testId, userId = id, reason = expired ? "expired" : "target_changed" })
                    });
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
