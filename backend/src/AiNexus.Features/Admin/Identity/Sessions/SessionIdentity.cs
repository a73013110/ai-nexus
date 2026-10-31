using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Identity;
using System.Globalization;
using System.Security.Claims;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Sessions;

public static class SessionIdentity
{
    public const string UserId = "user_id";
    public const string Version = "security_version";
    public const string Method = "auth_method";
    public const string DisplayName = "display_name";
    public const string ActorId = RequestClaims.ActorId;
    public const string ActorVersion = "actor_version";
    public const string ActorMethod = "actor_method";
    public const string ActorName = "actor_name";
    public const string TestExpires = "test_expires";
    public const string TestId = "test_id";
    public const string ActiveIdentity = "active_identity";
    /// <summary>LDAP 登入時暫時帶給 <see cref="CurrentUser"/> 的目錄信箱；<see cref="Principal"/> 不帶它，不會寫進 cookie。</summary>
    public const string DirectoryEmail = "directory_email";
    /// <summary>最近一次步升驗證（重新輸入密碼或驗證碼）的 Unix 秒數；敏感的帳號變更要求它在有效期限內。</summary>
    public const string Reauthenticated = "reauthenticated_at";
    public const string Restored = "Identity.Restored";
    /// <summary>測試身分結束的稽核動作；一次測試（<see cref="TestId"/>）最多一筆，也是 cookie 是否已撤銷的判斷依據。</summary>
    public static readonly string TestEndAction = IdentityAudit.TestIdentityEnded.Name;
    private const string Validated = "Identity.ValidatedUser";

    /// <summary>
    /// 取走此請求的 cookie 驗證時讀到的使用者列（未追蹤）；只有該使用者仍是目前登入者時才回傳。
    /// 只能取一次：同一請求之後再解析（例如長串流在新 scope 重驗）必須重查資料庫，否則看不到期間的停用或安全版本變更。
    /// </summary>
    internal static User? TakeValidatedUser(HttpContext http, Guid id)
    {
        if (http.Items[Validated] is not User user) return null;
        http.Items.Remove(Validated);
        return user.Id == id ? user : null;
    }

    /// <summary>principal 是否為測試身分（帶有來源管理者）。</summary>
    public static bool IsTest(ClaimsPrincipal? principal) => principal?.HasClaim(x => x.Type == ActorId) == true;

    public static ClaimsPrincipal Principal(User user, string method, User? actor = null, string? actorMethod = null, DateTimeOffset? testExpires = null, Guid? testId = null)
    {
        var claims = new List<Claim>
        {
            new(UserId, user.Id.ToString()), new(Version, user.SecurityVersion.ToString(CultureInfo.InvariantCulture)),
            new(Method, method), new(ClaimTypes.PrimarySid, user.Sid), new(ClaimTypes.Name, UserAccounts.LoginName(user)), new(DisplayName, user.DisplayName)
        };
        if (actor is not null)
        {
            claims.Add(new(ActorId, actor.Id.ToString())); claims.Add(new(ActorVersion, actor.SecurityVersion.ToString(CultureInfo.InvariantCulture)));
            claims.Add(new(ActorMethod, actorMethod!)); claims.Add(new(TestExpires, testExpires!.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
            claims.Add(new(ActorName, actor.DisplayName));
            claims.Add(new(TestId, (testId ?? Guid.NewGuid()).ToString()));
        }
        return new(new ClaimsIdentity(claims, AuthEndpoints.CookieScheme));
    }

    public static bool Allows(User user, string? method) => ActiveUsers.IsActive(user) && (method switch
    {
        "local" => user.LocalEnabled && user.PasswordHash is not null,
        "ad" or "windows" => user.AdEnabled,
        _ => false
    });
    public static bool MatchesVersion(User user, string? version) => int.TryParse(version, out var value) && value == user.SecurityVersion;

    /// <summary>
    /// session 對這位使用者是否仍有效：在職且安全版本相符；一般登入另要求當初的登入方式仍啟用，測試身分的登入方式屬於來源管理者，不檢查目標。
    /// cookie 驗證與 <see cref="CurrentUser"/> 共用同一判斷。
    /// </summary>
    public static bool Accepts(User user, ClaimsPrincipal principal) => MatchesVersion(user, principal.FindFirstValue(Version))
        && (IsTest(principal) ? ActiveUsers.IsActive(user) : Allows(user, principal.FindFirstValue(Method)));

    /// <summary>來源管理者是否仍可回到自己的身分：登入方式、安全版本與管理權都要仍有效。cookie 驗證與返回端點共用同一判斷。</summary>
    internal static async Task<bool> ActorStillValidAsync(User? actor, ClaimsPrincipal principal, AccessService access, CancellationToken ct)
        => actor is not null && Allows(actor, principal.FindFirstValue(ActorMethod)) && MatchesVersion(actor, principal.FindFirstValue(ActorVersion))
            && await access.HoldsAsync(actor.Id, FeatureCatalog.IdentityUsers, ct);

    /// <summary>直接用已建索引的 ResourceId 欄位查稽核紀錄判斷測試身分是否已結束，不解析 DetailsJson。測試識別碼是隨機產生的，不必再比對擁有者。</summary>
    internal static Task<bool> TestEndedAsync(AppDbContext db, Guid testId, CancellationToken ct)
        => db.AuditEvents.AsNoTracking().AnyAsync(x => x.ResourceId == testId && x.Action == TestEndAction && x.Result == AuditEvent.Completed, ct);

    /// <summary>
    /// 記下測試身分已結束（沒有請求稽核的路徑：cookie 驗證、登入與登出）。擁有者與執行者都是來源管理者、對象是測試識別碼，
    /// <see cref="TestEndedAsync"/> 依此判斷；<paramref name="reason"/> 區分過期、目標帳號變更、重新登入與登出。
    /// </summary>
    internal static void RecordTestEnded(AuditTrail audit, Guid actorId, Guid testId, Guid targetId, string reason)
        => audit.Record(IdentityAudit.TestIdentityEnded, actorId, testId, targetId, new { testId, userId = targetId, reason }).ActorId = actorId;

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var principal = context.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(UserId), out var id))
        {
            // 舊版（僅 AD 登入）發出的 cookie 沒有 UserId，改以 SID 對應；只有仍可 AD 登入且安全版本為 0 的使用者才繼續有效。
            var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
            var legacy = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, ct);
            if (legacy is null || (Allows(legacy, "ad") && legacy.SecurityVersion == 0)) return;
            await RejectAsync(context);
            return;
        }
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (Guid.TryParse(principal.FindFirstValue(ActorId), out var actorId))
        {
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
            if (!await ActorStillValidAsync(actor, principal, http.RequestServices.GetRequiredService<AccessService>(), ct)
                || !Guid.TryParse(principal.FindFirstValue(TestId), out var testId))
            {
                await RejectAsync(context);
                return;
            }
            var ended = await TestEndedAsync(db, testId, ct);
            var expired = !long.TryParse(principal.FindFirstValue(TestExpires), out var seconds) || seconds <= http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds();
            if (ended || expired || user is null || !Accepts(user, principal))
            {
                // 只切回仍有權限的原管理者，且絕不延長測試身分的有效期。
                context.ReplacePrincipal(Principal(actor!, principal.FindFirstValue(ActorMethod)!)); context.ShouldRenew = true;
                http.Items[Restored] = true;
                context.Properties.AllowRefresh = true;
                if (!ended)
                {
                    RecordTestEnded(http.RequestServices.GetRequiredService<AuditTrail>(), actorId, testId, id, expired ? "expired" : "target_changed");
                    await db.SaveChangesAsync(ct);
                }
                return;
            }
            http.Items[Validated] = user;
            return;
        }
        if (user is null || !Accepts(user, principal))
        {
            await RejectAsync(context);
            return;
        }
        http.Items[Validated] = user;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthEndpoints.CookieScheme);
    }
}
