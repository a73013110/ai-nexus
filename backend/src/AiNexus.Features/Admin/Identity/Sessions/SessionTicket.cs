using System.Globalization;
using System.Security.Claims;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Sessions;

/// <summary>
/// session 的期限與鎖定狀態。<c>LockedBy</c> 為 <c>idle</c>、<c>reauthentication</c> 或 <c>manual</c>，未鎖定時為 null。
/// <c>SecondsUntilLock</c> 是兩個期限中較近的一個；另外給 <c>SecondsUntilReauthentication</c>，前端才分得出操作能不能延長。
/// </summary>
public sealed record SessionLifetimeDto(string? LockedBy, int SecondsUntilLock, int IdleLockMinutes, int ReauthenticationHours, int SecondsUntilReauthentication);

/// <summary>
/// session cookie 的唯一簽發入口，也是期限的唯一判定處。兩個時間點放在票證的 Items 而不是 claims：
/// 防偽 token 綁定 claims，活動時間每分鐘都在變，放進 claims 會讓 CSRF token 一直失效。
/// 不用 cookie 的滑動到期：它只看「有請求」，背景輪詢會讓閒置的畫面永遠不過期。
/// </summary>
[Singleton]
public sealed class SessionTicket(IOptions<SessionLifetimeOptions> options, TimeProvider clock)
{
    public const string Idle = "idle";
    public const string Reauthentication = "reauthentication";
    public const string Manual = "manual";
    private const string LastActive = "last_active";
    private const string AuthenticatedAt = "authenticated_at";
    private const string ManualLock = "manual_lock";
    private const string Current = "Identity.SessionTicket";

    /// <summary>剛驗過密碼（登入、解鎖）：兩個計時與硬上限都重新起算。</summary>
    public Task IssueAsync(HttpContext http, ClaimsPrincipal principal)
    {
        var now = clock.GetUtcNow();
        return SignInAsync(http, principal, now, now, now.AddHours(options.Value.HardLimitHours));
    }

    /// <summary>換 principal（切換身分、測試身分、安全版本更新）：沿用所有期限，換發不能延長登入。</summary>
    public Task ReissueAsync(HttpContext http, ClaimsPrincipal principal)
    {
        if (Of(http) is not { } previous) return IssueAsync(http, principal);
        var (active, authenticated) = Times(previous);
        return SignInAsync(http, principal, active, authenticated, previous.ExpiresUtc, IsManuallyLocked(previous));
    }

    /// <summary>
    /// 使用者有操作：只把閒置計時往後推。沒有 cookie 的 Windows 請求不需要。
    /// 回報有節流，<paramref name="idleSeconds"/> 是最後一次操作距今多久，閒置從那一刻起算。
    /// 只能往回扣、也不早於上次活動，竄改也只會讓自己更早鎖定。
    /// </summary>
    public Task TouchAsync(HttpContext http, int idleSeconds = 0)
    {
        if (Of(http) is not { } previous) return Task.CompletedTask;
        var (active, authenticated) = Times(previous);
        var interacted = clock.GetUtcNow().AddSeconds(-Math.Max(0, idleSeconds));
        return SignInAsync(http, http.User, interacted > active ? interacted : active, authenticated, previous.ExpiresUtc);
    }

    /// <summary>使用者手動鎖定：期限都不變，只有輸入密碼（<see cref="IssueAsync"/>）會換掉這張票。</summary>
    public Task LockAsync(HttpContext http)
    {
        if (Of(http) is not { } previous) return Task.CompletedTask;
        var (active, authenticated) = Times(previous);
        return SignInAsync(http, http.User, active, authenticated, previous.ExpiresUtc, manuallyLocked: true);
    }

    /// <summary>cookie 驗證時記下這張票，同一請求後續的換發與描述都讀它，不必再驗一次 cookie。</summary>
    public static void Remember(HttpContext http, AuthenticationProperties ticket) => http.Items[Current] = ticket;

    /// <summary>票證鎖定的原因；未鎖定時為 null。手動鎖定優先（離開時間再長，回來看到的都是自己鎖的）；期限都到時說重新驗證：那才是這次必須輸入密碼的原因。</summary>
    public string? LockedBy(AuthenticationProperties ticket)
    {
        if (IsManuallyLocked(ticket)) return Manual;
        var now = clock.GetUtcNow();
        var (idle, reauthentication) = Deadlines(ticket);
        return now >= reauthentication ? Reauthentication : now >= idle ? Idle : null;
    }

    /// <summary>目前請求的期限；沒有 cookie（Windows 整合驗證、匿名）時為 null。</summary>
    public SessionLifetimeDto? Describe(HttpContext http)
    {
        if (Of(http) is not { } ticket) return null;
        var (idle, reauthentication) = Deadlines(ticket);
        var now = clock.GetUtcNow();
        return new(LockedBy(ticket), SecondsUntil(now, idle < reauthentication ? idle : reauthentication), options.Value.IdleLockMinutes, options.Value.ReauthenticationHours, SecondsUntil(now, reauthentication));
    }

    private static int SecondsUntil(DateTimeOffset now, DateTimeOffset deadline) => Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds));

    private static AuthenticationProperties? Of(HttpContext http) => http.Items[Current] as AuthenticationProperties;

    private static bool IsManuallyLocked(AuthenticationProperties ticket) => ticket.GetString(ManualLock) is not null;

    private (DateTimeOffset Idle, DateTimeOffset Reauthentication) Deadlines(AuthenticationProperties ticket)
    {
        var (active, authenticated) = Times(ticket);
        return (active.AddMinutes(options.Value.IdleLockMinutes), authenticated.AddHours(options.Value.ReauthenticationHours));
    }

    // 這項功能上線前發出的 cookie 沒有這兩個值，以簽發時間起算，不必讓所有人重新登入。
    private (DateTimeOffset Active, DateTimeOffset Authenticated) Times(AuthenticationProperties ticket)
    {
        var issued = ticket.IssuedUtc ?? clock.GetUtcNow();
        return (Read(ticket, LastActive) ?? issued, Read(ticket, AuthenticatedAt) ?? issued);
    }

    private static DateTimeOffset? Read(AuthenticationProperties ticket, string key)
        => long.TryParse(ticket.GetString(key), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

    private static string Stamp(DateTimeOffset value) => value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    private static async Task SignInAsync(HttpContext http, ClaimsPrincipal principal, DateTimeOffset active, DateTimeOffset authenticated, DateTimeOffset? expires, bool manuallyLocked = false)
    {
        var ticket = new AuthenticationProperties { IsPersistent = false, ExpiresUtc = expires };
        ticket.SetString(LastActive, Stamp(active));
        ticket.SetString(AuthenticatedAt, Stamp(authenticated));
        if (manuallyLocked) ticket.SetString(ManualLock, "1");
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, ticket);
        http.User = principal;
        Remember(http, ticket);
    }
}
