using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Security;

public static class TransportSecurity
{
    /// <summary>HSTS and HTTPS everywhere; plain HTTP is only accepted on loopback when development explicitly allows it.</summary>
    public static WebApplication UseTransportSecurity(this WebApplication app)
    {
        var testing = app.Environment.IsEnvironment("Testing");
        var localHttp = WebSecurity.AllowsLocalHttp(app.Environment, app.Configuration);
        if (!app.Environment.IsDevelopment() && !testing) app.UseHsts();
        if (!testing && !localHttp) app.UseHttpsRedirection();
        app.Use(async (http, next) =>
        {
            if (!http.Request.IsHttps && !testing && (!localHttp || !WebSecurity.IsLoopback(http.Request, http.Connection.RemoteIpAddress)))
                throw new ApiException(400, "https_required", "");
            await next(http);
        });
        return app;
    }
}
