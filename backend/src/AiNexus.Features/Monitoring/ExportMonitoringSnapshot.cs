using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

/// <summary>The snapshot as a JSON download, under the export rate limit. The export is audited before it is released.</summary>
internal sealed class ExportMonitoringSnapshot(RuntimeTraffic traffic, NexusDbContext db)
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/export", (int? minutes, ICurrentUser user, HttpContext http, ExportMonitoringSnapshot handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, minutes ?? 5, ct).ToHttpResultAsync(snapshot =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web), "application/json", "ai-nexus-monitoring.json");
            }))
        .RequireRateLimiting(AiNexus.Features.Diagnostics.DiagnosticsModule.ExportRateLimit).WithName("ExportMonitoringSnapshot");

    public async Task<Result<MonitoringSnapshot>> HandleAsync(Guid user, int window, CancellationToken ct)
    {
        if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow;
        var snapshot = traffic.Snapshot(window);
        await MonitoringReads.AuditAsync(db, user, "monitoring.export", ct);
        return snapshot;
    }
}
