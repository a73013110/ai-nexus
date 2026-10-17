using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Monitoring.Snapshots;

/// <summary>
/// 每位使用者最多 <see cref="PerUser"/> 個即時串流。新串流會取代該使用者最舊的一個，而不是拒絕它：重新整理後舊串流可能仍掛著，直到伺服器察覺瀏覽器已離開（代理伺服器可能隱藏這件事，直到串流逾時）；這段時間若拒絕，頁面會被鎖在外面。
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
                // 在鎖內取消：若放到鎖外，被取代串流的 lease 可能同時釋放同一個來源，造成競態。
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

    /// <summary>一個開啟中的串流；同一使用者有較新的串流取代它時，<see cref="Superseded"/> 會被觸發。</summary>
    public sealed class Lease(MonitoringStreams owner, Guid user, CancellationTokenSource source) : IDisposable
    {
        public CancellationToken Superseded { get; } = source.Token;
        public void Dispose() => owner.Exit(user, source);
    }
}

/// <summary>
/// 以 Server-Sent Events 推送快照，最長十分鐘；使用者的第四個串流會以 <c>stream_superseded</c> 結束最舊的一個。整個檢視只稽核一次；每 15 秒以新的 scope 重新檢查監控權限與測試身分的管理者狀態，因此權限被撤銷時，串流會以錯誤事件結束。
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
            // 關閉代理（如 nginx）與 ASP.NET 的回應緩衝，否則事件會被攢住，頁面收不到即時快照。
            http.Response.Headers.CacheControl = "no-store"; http.Response.Headers["X-Accel-Buffering"] = "no";
            http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RefreshSeconds), clock);
            var expires = clock.GetUtcNow().AddMinutes(10); var nextCheck = DateTimeOffset.MinValue;
            do
            {
                if (clock.GetUtcNow() >= nextCheck)
                {
                    // 每次都以新的 scope 重新讀取帳號版本與權限；長時間存活的串流必須遵守撤銷。
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
