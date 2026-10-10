using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

/// <summary>This instance's live sessions, traffic and dependencies over the last 1, 5 or 15 minutes. The read is audited.</summary>
internal sealed class GetMonitoringSnapshot(RuntimeTraffic traffic, NexusDbContext db)
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
        var snapshot = traffic.Snapshot(window);
        await MonitoringReads.AuditAsync(db, user, "monitoring.snapshot", ct);
        return snapshot;
    }
}
