using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Identity;

/// <summary>
/// Per-user fixed windows for endpoints whose cost one user can multiply: sending, uploading, log queries. Each policy
/// keeps its own counters. The limiter runs before the user row is resolved, so the key comes from the authenticated
/// principal: the session's user id (sign-in cookie), else the Windows SID; only unauthenticated requests share their
/// client address's window. Rejections answer 429 <c>rate_limited</c> with <c>Retry-After</c>.
/// </summary>
public static class UserRateLimits
{
    public static RateLimiterOptions AddPerUserLimit(this RateLimiterOptions options, string policy, int permitsPerMinute)
        => options.AddPolicy(policy, http => RateLimitPartition.GetFixedWindowLimiter(Partition(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));

    private static string Partition(HttpContext http)
    {
        var user = http.User;
        if (user.Identity?.IsAuthenticated == true && (user.FindFirstValue(SessionIdentity.UserId) ?? user.FindFirstValue(ClaimTypes.PrimarySid)) is { Length: > 0 } id)
            return "user:" + id;
        return "address:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
