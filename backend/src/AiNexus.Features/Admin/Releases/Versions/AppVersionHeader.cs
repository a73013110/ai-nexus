namespace AiNexus.Features.Admin.Releases.Versions;

/// <summary>
/// 登入後的 API 回應帶 <c>X-App-Version</c>：前端與自己的 <c>APP_VERSION</c> 不同時提示重新整理（ADR 0028）。
/// 未登入的回應不帶，避免對外揭露版本。
/// </summary>
public static class AppVersionHeader
{
    public const string Name = "X-App-Version";

    /// <summary>放在 <c>UseAuthentication</c> 之後，才知道是否已登入。</summary>
    public static IApplicationBuilder UseAppVersionHeader(this IApplicationBuilder app)
    {
        var version = app.ApplicationServices.GetRequiredService<AppRelease>().Version;
        return app.Use((http, next) =>
        {
            if (http.Request.Path.StartsWithSegments("/api") && http.User.Identity?.IsAuthenticated == true)
            {
                http.Response.Headers[Name] = version;
            }
            return next(http);
        });
    }
}
