using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Admin.Identity;

/// <summary>
/// 依使用者分區的固定視窗限流，給「單一使用者能放大成本」的端點用，例如傳送訊息、上傳、日誌查詢。每個 policy 各自計數。
/// 限流器在解析使用者列之前執行，所以 key 只能取自已驗證的 principal：session 的使用者 id（登入 cookie），否則為 Windows SID；
/// 只有未驗證的請求才共用來源位址的視窗。被拒絕時回 429 <c>rate_limited</c> 並帶 <c>Retry-After</c>。
/// </summary>
public static class UserRateLimits
{
    public static RateLimiterOptions AddPerUserLimit(this RateLimiterOptions options, string policy, int permitsPerMinute)
        => options.AddPolicy(policy, http => RateLimitPartition.GetFixedWindowLimiter(Partition(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));

    private static string Partition(HttpContext http)
    {
        var user = http.User;
        // 未驗證的請求拿不到可信的 user id，只能退回來源位址；不可用未驗證的 header 或 cookie 當 key。
        var id = user.FindFirstValue(SessionIdentity.UserId) ?? user.FindFirstValue(ClaimTypes.PrimarySid);
        if (user.Identity?.IsAuthenticated == true && id is { Length: > 0 })
            return "user:" + id;
        return "address:" + (http.ClientAddress()?.ToString() ?? "unknown");
    }
}
