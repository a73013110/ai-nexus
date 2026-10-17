using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity.Sessions;

public static class IdentityConsistency
{
    /// <summary>
    /// 在每個 <c>/api/v1</c> 回應加上 <c>X-Nexus-Identity</c> 標頭，告知實際服務的身分；若請求是在測試身分被切回管理者的途中送出，則拒絕執行。
    /// </summary>
    public static IApplicationBuilder UseIdentityConsistency(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        http.Response.OnStarting(() =>
        {
            if (http.Request.Path.StartsWithSegments("/api/v1") && http.User.Identity?.IsAuthenticated == true)
            {
                var id = http.User.FindFirst(SessionIdentity.UserId)?.Value ?? http.RequestServices.GetRequiredService<CurrentUser>().ResolvedId?.ToString();
                if (id is not null) http.Response.Headers["X-Nexus-Identity"] = id + ":" + http.User.FindFirst(SessionIdentity.ActorId)?.Value;
            }
            return Task.CompletedTask;
        });
        if (http.Items.ContainsKey(SessionIdentity.Restored) && http.Request.Path.StartsWithSegments("/api/v1") &&
            !http.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            // 這個請求是以測試目標身分送出的，但 cookie 驗證時已切回管理者；不能改在管理者身分下執行，所以回 identity_changed。
            await Problems.WriteAsync(http, Problems.Status(IdentityErrors.IdentityChanged.Kind), IdentityErrors.IdentityChanged.Code);
            return;
        }
        await next(http);
    });
}
