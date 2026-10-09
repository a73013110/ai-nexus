using System.Diagnostics;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Monitoring;

public sealed class RuntimeTrafficMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, RuntimeTraffic traffic)
    {
        var path = http.Request.Path;
        if (path.StartsWithSegments("/api/v1/presence") || path.StartsWithSegments("/api/v1/admin/monitoring"))
        { using var suppression = new MonitoringSuppression(); await next(http); return; }
        if (!traffic.Enabled || !path.StartsWithSegments("/api/v1")) { await next(http); return; }
        var stream = false;
        traffic.RequestStarted(false);
        http.Response.OnStarting(() => {
            if (http.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
            { stream = true; traffic.StreamStarted(); }
            return Task.CompletedTask;
        });
        var started = Stopwatch.GetTimestamp();
        var originalRequest = http.Request.Body; var originalResponse = http.Response.Body;
        var request = new TrafficCountingStream(originalRequest, n => traffic.Transferred(n, 0));
        var response = new TrafficCountingStream(originalResponse, written: n => traffic.Transferred(0, n));
        http.Request.Body = request; http.Response.Body = response;
        try { await next(http); }
        finally
        {
            http.Request.Body = originalRequest; http.Response.Body = originalResponse;
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/api/v1/{unmatched}";
            var principal = http.User;
            Guid? user = http.RequestServices.GetService<CurrentUser>()?.ResolvedId ??
                (principal.Identity?.IsAuthenticated == true && Guid.TryParse(principal.FindFirst(SessionIdentity.UserId)?.Value, out var id) ? id : null);
            Guid? session = Guid.TryParse(http.Request.Headers["X-Nexus-Session"], out var sid) ? sid : null;
            traffic.RequestFinished(user, session, http.Request.Method, route, http.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                request.ReadBytes, response.WrittenBytes, stream, http.RequestAborted.IsCancellationRequested, http.Items["Nexus.TraceId"] as string);
        }
    }
}
