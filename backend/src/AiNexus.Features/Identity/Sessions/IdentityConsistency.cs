using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity.Sessions;

public static class IdentityConsistency
{
    /// <summary>
    /// Tells the client which identity served each API response, and refuses work submitted under an identity that was
    /// switched back (administrator test identity) mid-flight.
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
            // An operation submitted as the target must never execute under the restored administrator.
            await Problems.WriteAsync(http, Problems.Status(IdentityErrors.IdentityChanged.Kind), IdentityErrors.IdentityChanged.Code);
            return;
        }
        await next(http);
    });
}
