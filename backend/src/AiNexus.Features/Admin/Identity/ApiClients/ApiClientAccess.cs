using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>
/// 外部系統以金鑰（<c>Authorization: Bearer ainexus_…</c>）呼叫的端點。端點只要宣告 <see cref="RequireApiClient"/>：
/// 驗證方式、每個系統的限流與 CSRF 的例外都由這裡一起決定，不會漏掉其中一項。
/// </summary>
/// <remarks>
/// 金鑰不是預設的驗證方式：一般端點不接受金鑰，宣告了 <see cref="RequireApiClient"/> 的端點也不接受 cookie。
/// 所以不檢查 CSRF——CSRF 防的是瀏覽器自動帶上的 cookie，金鑰必須由呼叫端明確寫進 header。
/// </remarks>
public static class ApiClientAccess
{
    public const string Scheme = "ApiClient";
    public const string Policy = "api-client";
    public const string RateLimit = "api-client";
    /// <summary>
    /// 每個外部系統每分鐘的請求上限。一個系統代表它所有的使用者（例如全公司早上同時登入公文系統），所以比單一使用者的上限寬；
    /// 每個帳號的猜密碼另由 <see cref="Authentication.LoginThrottle"/> 計數。
    /// </summary>
    public const int RequestsPerMinute = 300;

    internal const string ClientIdClaim = "api_client_id";
    internal const string ClientNameClaim = "api_client_name";

    public static TBuilder RequireApiClient<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(Policy).RequireRateLimiting(RateLimit).DisableAntiforgery();

    /// <summary>呼叫的外部系統（<c>identity.ApiClients</c> 的主鍵）；只在宣告 <see cref="RequireApiClient"/> 的端點可用。</summary>
    public static Guid ApiClientId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClientIdClaim) ?? throw new InvalidOperationException("The endpoint does not require an API client."), CultureInfo.InvariantCulture);

    /// <summary>呼叫的外部系統在驗證當下的名稱，供稽核記下是哪個系統。</summary>
    public static string ApiClientName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClientNameClaim) ?? throw new InvalidOperationException("The endpoint does not require an API client.");

    internal static void Add(IServiceCollection services, AuthenticationBuilder authentication)
    {
        authentication.AddScheme<AuthenticationSchemeOptions, ApiClientAuthenticationHandler>(Scheme, null);
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy.AddAuthenticationSchemes(Scheme).RequireClaim(ClientIdClaim));
        services.AddRateLimiter(options => options.AddPolicy(RateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
            // 授權已在限流之前完成，到這裡的請求都有可信的系統識別碼。
            http.User.FindFirstValue(ClientIdClaim) ?? "",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
    }
}
