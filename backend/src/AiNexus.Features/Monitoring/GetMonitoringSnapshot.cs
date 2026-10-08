using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

/// <summary>This instance's live sessions, traffic and dependencies over the last 1, 5 or 15 minutes. The read is audited.</summary>
internal static class GetMonitoringSnapshot
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("", async (int? minutes, RuntimeTraffic traffic, HttpContext http, ICurrentUser user, NexusDbContext db, CancellationToken ct) => {
            var window = minutes ?? 5;
            if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow.ToProblem();
            var snapshot = traffic.Snapshot(window); await MonitoringReads.AuditAsync(db, user.Id, "monitoring.snapshot", ct);
            http.Response.Headers.CacheControl = "no-store"; return Results.Ok(snapshot);
        }).WithName("GetMonitoringSnapshot").Produces<MonitoringSnapshot>();
}
