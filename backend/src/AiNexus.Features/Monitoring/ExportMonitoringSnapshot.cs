using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

/// <summary>The snapshot as a JSON download, under the export rate limit. The export is audited before it is released.</summary>
internal static class ExportMonitoringSnapshot
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/export", async (int? minutes, RuntimeTraffic traffic, ICurrentUser user, NexusDbContext db, HttpContext http, CancellationToken ct) => {
            var window = minutes ?? 5;
            if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow.ToProblem();
            var snapshot = traffic.Snapshot(window);
            await MonitoringReads.AuditAsync(db, user.Id, "monitoring.export", ct);
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web), "application/json", "ai-nexus-monitoring.json");
        }).RequireRateLimiting(AiNexus.Features.Diagnostics.DiagnosticsModule.ExportRateLimit).WithName("ExportMonitoringSnapshot");
}
