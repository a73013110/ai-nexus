using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>
/// 每位使用者最多 <see cref="PerUser"/> 個即時串流。超過時取代最舊的一個，而不是拒絕新串流。
/// 原因是重新整理後，舊串流可能還掛著，要等伺服器察覺瀏覽器已離開（代理伺服器可能會隱藏這件事，直到串流逾時）。
/// 這段時間若拒絕，新頁面就會被擋在外面。
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
/// 以 Server-Sent Events 推送快照，最長十分鐘。使用者的第四個串流會以 <c>stream_superseded</c> 結束最舊的一個。
/// 整個檢視只稽核一次（開始推送前）。每 15 秒以新的 scope 重新檢查監控權限與測試身分的管理者狀態（<see cref="StillAllowedAsync"/>），
/// 所以權限被撤銷後，串流會以錯誤事件結束，不會一直沿用舊的授權。
/// </summary>
internal sealed class StreamMonitoringSnapshots(MonitoringSnapshots snapshots, MonitoringStreams streams, AuditTrail audit, IServiceScopeFactory scopes, IOptions<MonitoringOptions> options, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/events", (int? minutes, HttpContext http, ICurrentUser user, StreamMonitoringSnapshots handler, CancellationToken ct) =>
            handler.HandleAsync(http, user.Id, minutes ?? MonitoringReads.DefaultWindow, ct).ToStreamResultAsync(http))
        .Audited(MonitoringAudit.SnapshotRead)
        .WithName("StreamMonitoringSnapshots").Produces<MonitoringSnapshot>(200, "text/event-stream");

    public async Task<Result> HandleAsync(HttpContext http, Guid user, int window, CancellationToken ct)
    {
        if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow;
        using var lease = streams.Enter(user);
        using var stream = CancellationTokenSource.CreateLinkedTokenSource(ct, lease.Superseded);
        try
        {
            ct = stream.Token;
            // 串流的回應不會回到請求稽核手上，開始推送前先定案這筆讀取稽核；寫不進去就不推送（失敗即拒絕）。
            await audit.CompleteAsync(AuditEvent.Completed);
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
                    var allowed = await StillAllowedAsync(http.User, ct);
                    if (!allowed.IsSuccess) return allowed;
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

    /// <summary>
    /// 以新的 scope 重新讀取帳號版本與權限；長時間存活的串流必須遵守撤銷。
    /// 測試身分另外要求來源管理者仍有效（與 cookie 驗證同一判斷）、測試未逾期也未被結束。
    /// </summary>
    private async Task<Result> StillAllowedAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var identity = await scope.ServiceProvider.GetRequiredService<CurrentUser>().GetAsync(ct);
        if (!identity.IsSuccess) return identity.Error;
        var access = scope.ServiceProvider.GetRequiredService<AccessService>();
        if (!await access.HoldsAsync(identity.Value.Id, MonitoringModule.Feature, ct)) return AccessControlErrors.FeatureForbidden;
        if (!Guid.TryParse(principal.FindFirstValue(SessionIdentity.ActorId), out var actorId)) return Result.Success;
        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = await scopedDb.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var revoked = !await SessionIdentity.ActorStillValidAsync(actor, principal, access, ct)
            || !long.TryParse(principal.FindFirstValue(SessionIdentity.TestExpires), out var testExpires) || testExpires <= clock.GetUtcNow().ToUnixTimeSeconds()
            || !Guid.TryParse(principal.FindFirstValue(SessionIdentity.TestId), out var testId)
            || await SessionIdentity.TestEndedAsync(scopedDb, testId, ct);
        return revoked ? MonitoringErrors.SessionRevoked : Result.Success;
    }
}
