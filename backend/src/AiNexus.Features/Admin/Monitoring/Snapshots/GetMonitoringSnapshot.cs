using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>本實例最近 1、5 或 15 分鐘的即時工作階段、流量與相依服務；讀取會被稽核。</summary>
internal sealed class GetMonitoringSnapshot(MonitoringSnapshots snapshots, AppDbContext db)
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("", (int? minutes, HttpContext http, ICurrentUser user, GetMonitoringSnapshot handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, minutes ?? 5, ct).ToHttpResultAsync(snapshot =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.Ok(snapshot);
            }))
        .WithName("GetMonitoringSnapshot");

    public async Task<Result<MonitoringSnapshot>> HandleAsync(Guid user, int window, CancellationToken ct)
    {
        if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow;
        var snapshot = snapshots.Read(window);
        await MonitoringReads.AuditAsync(db, user, "monitoring.snapshot", ct);
        return snapshot;
    }
}
