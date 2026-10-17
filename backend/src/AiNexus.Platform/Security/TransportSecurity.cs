using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Security;

public static class TransportSecurity
{
    /// <summary>全域強制啟用 HSTS 與 HTTPS 傳輸安全；僅在開發環境明確配置且來源為 Loopback（本機迴路）時才允許純 HTTP 連線。</summary>
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
