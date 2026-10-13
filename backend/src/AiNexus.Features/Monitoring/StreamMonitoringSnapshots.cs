using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Monitoring;

/// <summary>
/// At most <see cref="PerUser"/> live streams per user. A new stream supersedes the user's oldest instead of being refused:
/// after a reload the old stream lingers until the server notices the browser left (a proxy may hide that until the
/// stream expires), and refusing would lock the page out meanwhile.
/// </summary>
public sealed class MonitoringStreams
{
    public const int PerUser = 3;
    private readonly Dictionary<Guid, List<CancellationTokenSource>> open = new();

    public Lease Enter(Guid user)
    {
        var source = new CancellationTokenSource();
        lock (open)
        {
            if (!open.TryGetValue(user, out var streams)) open[user] = streams = [];
            if (streams.Count >= PerUser)
            {
                // Cancelled under the lock, so the superseded stream's lease cannot dispose the source meanwhile.
                streams[0].Cancel();
                streams.RemoveAt(0);
            }
            streams.Add(source);
        }
        return new(this, user, source);
    }

    private void Exit(Guid user, CancellationTokenSource source)
    {
        lock (open)
        {
            if (open.TryGetValue(user, out var streams) && streams.Remove(source) && streams.Count == 0) open.Remove(user);
            source.Dispose();
        }
    }

    /// <summary>One open stream; <see cref="Superseded"/> fires when a newer stream of the same user takes its place.</summary>
    public sealed class Lease(MonitoringStreams owner, Guid user, CancellationTokenSource source) : IDisposable
    {
        public CancellationToken Superseded { get; } = source.Token;
        public void Dispose() => owner.Exit(user, source);
    }
}

/// <summary>
/// Server-sent snapshots for up to ten minutes; a user's fourth stream ends the oldest with <c>stream_superseded</c>. The view
/// is audited once; every 15 seconds a fresh scope re-checks the monitoring grant and a testing identity's administrator, so
/// revocation ends the stream with an error event.
/// </summary>
internal sealed class StreamMonitoringSnapshots(MonitoringSnapshots snapshots, MonitoringStreams streams, NexusDbContext db, IServiceScopeFactory scopes, IOptions<MonitoringOptions> options, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/events", (int? minutes, HttpContext http, ICurrentUser user, StreamMonitoringSnapshots handler, CancellationToken ct) =>
            handler.HandleAsync(http, user.Id, minutes ?? 5, ct).ToStreamResultAsync(http))
        .WithName("StreamMonitoringSnapshots").Produces<MonitoringSnapshot>(200, "text/event-stream");

    public async Task<Result> HandleAsync(HttpContext http, Guid user, int window, CancellationToken ct)
    {
        if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow;
        using var lease = streams.Enter(user);
        using var stream = CancellationTokenSource.CreateLinkedTokenSource(ct, lease.Superseded);
        try
        {
            ct = stream.Token;
            await MonitoringReads.AuditAsync(db, user, "monitoring.view", ct);
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-store"; http.Response.Headers["X-Accel-Buffering"] = "no";
            http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RefreshSeconds), clock);
            var expires = clock.GetUtcNow().AddMinutes(10); var nextCheck = DateTimeOffset.MinValue;
            do
            {
                if (clock.GetUtcNow() >= nextCheck)
                {
                    // Fresh scopes re-read account versions and grants; a long-lived stream must honor revocation.
                    await using var scope = scopes.CreateAsyncScope();
                    var identity = await scope.ServiceProvider.GetRequiredService<CurrentUser>().GetAsync(ct);
                    if (!identity.IsSuccess) return identity.Error;
                    var access = scope.ServiceProvider.GetRequiredService<AccessService>();
                    if (!(await access.ForUserAsync(identity.Value.Id, ct)).Features.Any(x => x.Id == MonitoringModule.Feature))
                        return AccessControlErrors.FeatureForbidden;
                    if (Guid.TryParse(http.User.FindFirstValue(SessionIdentity.ActorId), out var actorId))
                    {
                        var actor = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
                        if (actor is null || !SessionIdentity.Allows(actor, http.User.FindFirstValue(SessionIdentity.ActorMethod)) || !SessionIdentity.MatchesVersion(actor, http.User.FindFirstValue(SessionIdentity.ActorVersion)))
                            return MonitoringErrors.SessionRevoked;
                        if (!long.TryParse(http.User.FindFirstValue(SessionIdentity.TestExpires), out var testExpires) || testExpires <= clock.GetUtcNow().ToUnixTimeSeconds() ||
                            !Guid.TryParse(http.User.FindFirstValue(SessionIdentity.TestId), out var testId) ||
                            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.AsNoTracking().AnyAsync(x => x.ResourceId == testId && x.OwnerId == actorId && x.Action == "identity.test_end", ct) ||
                            !(await access.ForUserAsync(actorId, ct)).Features.Any(x => x.Id == FeatureIds.Admin))
                            return MonitoringErrors.SessionRevoked;
                    }
                    nextCheck = clock.GetUtcNow().AddSeconds(15);
                }
                await http.Response.WriteAsync("event: snapshot\ndata: " + JsonSerializer.Serialize(snapshots.Read(window), JsonSerializerOptions.Web) + "\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            } while (clock.GetUtcNow() < expires && await timer.WaitForNextTickAsync(ct));
            return Result.Success;
        }
        catch (OperationCanceledException) when (lease.Superseded.IsCancellationRequested && !http.RequestAborted.IsCancellationRequested)
        {
            return MonitoringErrors.StreamSuperseded;
        }
    }
}
