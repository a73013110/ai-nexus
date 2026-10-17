using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Security;

/// <summary>套用 HSTS 與 HTTPS 強制轉址，並拒絕非 HTTPS 的請求。</summary>
public static class TransportSecurity
{
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
                await Problems.WriteAsync(http, StatusCodes.Status400BadRequest, "https_required");
                return;
            }
            await next(http);
        });
        return app;
    }
}
