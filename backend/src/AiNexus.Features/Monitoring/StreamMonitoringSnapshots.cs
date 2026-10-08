using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Monitoring;

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

/// <summary>
/// Server-sent snapshots for up to ten minutes, at most three streams per user. The view is audited once; every 15 seconds
/// a fresh scope re-checks the monitoring grant and a testing identity's administrator, so revocation ends the stream.
/// Failures after the stream starts can only end it, so they stay exceptions.
/// </summary>
internal static class StreamMonitoringSnapshots
{
    // A method group, unlike the other slices' lambdas; the published contract was generated from it.
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/events", StreamAsync).WithName("StreamMonitoringSnapshots").Produces<MonitoringSnapshot>(200, "text/event-stream");

    private static async Task StreamAsync(int? minutes, HttpContext http, RuntimeTraffic traffic, MonitoringStreams streams,
        ICurrentUser user, NexusDbContext db, IServiceScopeFactory scopes, IOptions<MonitoringOptions> options, CancellationToken ct)
    {
        var window = minutes ?? 5; traffic.Snapshot(window); // Validate before sending headers.
        if (!streams.Enter(user.Id)) throw new ApiException(429, "rate_limited", "");
        try
        {
            await MonitoringReads.AuditAsync(db, user.Id, "monitoring.view", ct);
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
                    if (!(await access.ForUserAsync(identity.Id, ct)).Features.Any(x => x.Id == MonitoringModule.Feature))
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
