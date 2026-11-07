using Microsoft.AspNetCore.Antiforgery;

namespace AiNexus.Platform.Security;

/// <summary>
/// 標頭權杖（Header-token）CSRF 防護機制，涵蓋所有變更狀態之 API 請求（包含登入端點）。
/// 唯一的例外是以 <c>DisableAntiforgery()</c> 宣告、只接受 header 憑證（外部系統金鑰）的端點：瀏覽器不會自動帶上那種憑證，CSRF 無從發生。
/// 哪些端點可以宣告由 EndpointConventionTests 檢查，cookie 端點不能借用。
/// </summary>
public static class Csrf
{
    public const string HeaderName = "X-CSRF-Token";

    public static IServiceCollection AddCsrfProtection(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
        => services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = "AiNexus.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = WebSecurity.CookiePolicy(environment, configuration);
        });

    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        if (http.Request.Path.StartsWithSegments("/api/v1") && http.Request.Method is not ("GET" or "HEAD" or "OPTIONS")
            && http.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>() is not { RequiresValidation: false })
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        await next(http);
    });
}
