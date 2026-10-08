using System.Security.Claims;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.BuildingBlocks.Diagnostics;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Monitoring;

public sealed class MonitoringStreams
{
    private readonly Dictionary<Guid, int> counts = new();
    public bool Enter(Guid user)
    {
        lock (counts) { var count = counts.GetValueOrDefault(user); if (count >= 3) return false; counts[user] = count + 1; return true; }
    }
    public void Exit(Guid user)
    { lock (counts) { var count = counts.GetValueOrDefault(user); if (count <= 1) counts.Remove(user); else counts[user] = count - 1; } }
}

public static class MonitoringEndpoints
{
    public const string Feature = "monitoring", Policy = "feature:monitoring";
    public static void AddMonitoring(this IServiceCollection services)
    {
        services.AddOptions<MonitoringOptions>().BindConfiguration("Monitoring").Validate(x => x.Valid(), "Invalid monitoring capacity or timing.").ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RuntimeTraffic>(); services.AddSingleton<DependencyCatalog>(); services.AddSingleton<MonitoringStreams>();
        services.AddTransient<TrafficHttpHandler>();
        services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<TrafficHttpHandler>());
        services.AddHostedService<RuntimeSampler>();
    }
    public static void MapMonitoring(this RouteGroupBuilder api)
    {
        api.MapPost("/presence", async (PresenceRequest request, HttpContext http, CurrentUser current, RuntimeTraffic traffic, CancellationToken ct) => {
            if (request.SessionId == Guid.Empty || !MonitoringVocabulary.ValidFeature(request.Feature) || request.State is not ("active" or "idle" or "background"))
                throw new ApiException(400, "invalid_request", "");
            var user = await current.GetAsync(ct);
            var ua = http.Request.Headers.UserAgent.ToString();
            var browser = ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge" : ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox" : ua.Contains("Chrome/", StringComparison.Ordinal) ? "Chrome" : ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari" : "其他瀏覽器";
            var device = ua.Contains("Android", StringComparison.Ordinal) ? "Android" : ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) ? "iOS" : ua.Contains("Windows", StringComparison.Ordinal) ? "Windows" : ua.Contains("Mac", StringComparison.Ordinal) ? "macOS" : ua.Contains("Linux", StringComparison.Ordinal) ? "Linux" : "未知裝置";
            var ip = http.Connection.RemoteIpAddress; if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();
            traffic.Presence(user, request, ip?.ToString() ?? "未知", browser, device,
                http.User.FindFirstValue(SessionIdentity.Method) is "local" ? "local" : "ad", http.User.HasClaim(x => x.Type == SessionIdentity.ActorId));
            return new PresenceReceipt(traffic.Enabled, 25);
        }).RequireRateLimiting("presence").WithMetadata(new SuppressSuccessfulRequestLog()).WithName("HeartbeatPresence").Produces<PresenceReceipt>();
        api.MapDelete("/presence/{sessionId:guid}", async (Guid sessionId, CurrentUser current, RuntimeTraffic traffic, CancellationToken ct) => {
            traffic.Leave((await current.GetAsync(ct)).Id, sessionId); return Results.NoContent();
        }).WithMetadata(new SuppressSuccessfulRequestLog()).WithName("LeavePresence");

        var monitor = api.MapGroup("/admin/monitoring").RequireAuthorization(Policy).RequireRateLimiting("diagnostic-query")
            .WithMetadata(new SuppressSuccessfulRequestLog()).WithTags("Runtime monitoring");
        monitor.MapGet("", async (int? minutes, RuntimeTraffic traffic, HttpContext http, CurrentUser current, NexusDbContext db, CancellationToken ct) => {
            var snapshot = traffic.Snapshot(minutes ?? 5); await AuditAsync("monitoring.snapshot", current, db, ct);
            http.Response.Headers.CacheControl = "no-store"; return snapshot;
        }).WithName("GetMonitoringSnapshot").Produces<MonitoringSnapshot>();
        monitor.MapGet("/export", async (int? minutes, RuntimeTraffic traffic, CurrentUser current, NexusDbContext db, HttpContext http, CancellationToken ct) => {
            var snapshot = traffic.Snapshot(minutes ?? 5);
            await AuditAsync("monitoring.export", current, db, ct);
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web), "application/json", "ai-nexus-monitoring.json");
        }).RequireRateLimiting("diagnostic-export").WithName("ExportMonitoringSnapshot");
        monitor.MapGet("/events", StreamAsync).WithName("StreamMonitoringSnapshots").Produces<MonitoringSnapshot>(200, "text/event-stream");
    }
    private static async Task AuditAsync(string action, CurrentUser current, NexusDbContext db, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        db.AuditEvents.Add(new() { OwnerId = user.Id, Action = action, ResourceId = user.Id, DetailsJson = "{\"scope\":\"current_instance\"}" });
        await db.SaveChangesAsync(ct);
    }
    private static async Task StreamAsync(int? minutes, HttpContext http, RuntimeTraffic traffic, MonitoringStreams streams,
        CurrentUser current, NexusDbContext db, IServiceScopeFactory scopes, IOptions<MonitoringOptions> options, CancellationToken ct)
    {
        var window = minutes ?? 5; traffic.Snapshot(window); // Validate before sending headers.
        var user = await current.GetAsync(ct);
        if (!streams.Enter(user.Id)) throw new ApiException(429, "rate_limited", "");
        try
        {
            await AuditAsync("monitoring.view", current, db, ct);
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-store"; http.Response.Headers["X-Accel-Buffering"] = "no";
            http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RefreshSeconds));
            var expires = DateTimeOffset.UtcNow.AddMinutes(10); var nextCheck = DateTimeOffset.MinValue;
            do
            {
                if (DateTimeOffset.UtcNow >= nextCheck)
                {
                    // Fresh scopes re-read account versions and grants; a long-lived stream must honor revocation.
                    await using var scope = scopes.CreateAsyncScope();
                    var identity = await scope.ServiceProvider.GetRequiredService<CurrentUser>().GetAsync(ct);
                    var access = scope.ServiceProvider.GetRequiredService<AccessService>();
                    if (!(await access.ForUserAsync(identity.Id, ct)).Features.Any(x => x.Id == Feature))
                        throw new ApiException(403, "feature_forbidden", "");
                    if (Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId))
                    {
                        var actor = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
                        if (actor is null || !SessionIdentity.Allows(actor, http.User.FindFirstValue(SessionIdentity.ActorMethod)) || !SessionIdentity.MatchesVersion(actor, http.User.FindFirstValue(SessionIdentity.ActorVersion)))
                            throw new ApiException(401, "session_revoked", "");
                        if (!long.TryParse(http.User.FindFirstValue(SessionIdentity.TestExpires), out var testExpires) || testExpires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                            !Guid.TryParse(http.User.FindFirstValue(SessionIdentity.TestId), out var testId) ||
                            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.AsNoTracking().AnyAsync(x => x.ResourceId == testId && x.OwnerId == actorId && x.Action == "identity.test_end", ct) ||
                            !(await access.ForUserAsync(actorId, ct)).Features.Any(x => x.Id == "admin"))
                            throw new ApiException(401, "session_revoked", "");
                    }
                    nextCheck = DateTimeOffset.UtcNow.AddSeconds(15);
                }
                await http.Response.WriteAsync("event: snapshot\ndata: " + JsonSerializer.Serialize(traffic.Snapshot(window), JsonSerializerOptions.Web) + "\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            } while (DateTimeOffset.UtcNow < expires && await timer.WaitForNextTickAsync(ct));
        }
        finally { streams.Exit(user.Id); }
    }
}
