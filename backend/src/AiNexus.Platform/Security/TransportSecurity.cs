using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.CookiePolicy;

namespace AiNexus.Platform.Security;

/// <summary>套用 HSTS 與 HTTPS 強制轉址、拒絕非 HTTPS 的請求，並統一所有 cookie 的安全屬性。</summary>
public static class TransportSecurity
{
    private static readonly Error HttpsRequired = Error.Invalid("https_required");

    /// <summary>
    /// 全站 cookie 一律 HttpOnly、Secure、SameSite=Strict，由 cookie 原則中介軟體在寫出時強制，
    /// 個別 cookie（工作階段、防偽）只設名稱；之後新增的 cookie 不必各自記得設定。
    /// IIS 的 web.config <c>httpCookies</c> 只作用於 ASP.NET（System.Web），對 ASP.NET Core 沒有效果，所以不在那裡設定。
    /// </summary>
    public static IServiceCollection AddTransportSecurity(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
        => services.Configure<CookiePolicyOptions>(options =>
        {
            options.HttpOnly = HttpOnlyPolicy.Always;
            options.Secure = WebSecurity.CookiePolicy(environment, configuration);
            options.MinimumSameSitePolicy = SameSiteMode.Strict;
        });

    /// <summary>全域強制 HSTS 與 HTTPS。只有開發環境明確設定且來源為 Loopback 時，才允許純 HTTP。</summary>
    public static WebApplication UseTransportSecurity(this WebApplication app)
    {
        var testing = app.Environment.IsEnvironment("Testing");
        var localHttp = WebSecurity.AllowsLocalHttp(app.Environment, app.Configuration);
        if (!app.Environment.IsDevelopment() && !testing) app.UseHsts();
        if (!testing && !localHttp) app.UseHttpsRedirection();
        app.Use(async (http, next) =>
        {
            if (!http.Request.IsHttps && !testing && (!localHttp || !WebSecurity.IsLoopback(http.Request, http.Connection.RemoteIpAddress)))
            {
                await Problems.WriteAsync(http, HttpsRequired);
                return;
            }
            await next(http);
        });
        // 必須在驗證與防偽之前：它們寫出或刪除的 cookie 都經過這裡套用原則。
        app.UseCookiePolicy();
        return app;
    }
}
