using AiNexus.Data.App;
using System.Text.Json;
using AiNexus.Features.Admin.Diagnostics;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>以 JSON 檔案下載即時監控快照，受匯出速率限制；釋出前會先寫入稽核紀錄。</summary>
internal sealed class ExportMonitoringSnapshot(MonitoringSnapshots snapshots, AppDbContext db)
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/export", (int? minutes, ICurrentUser user, HttpContext http, ExportMonitoringSnapshot handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, minutes ?? 5, ct).ToHttpResultAsync(snapshot =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web), "application/json", "ai-nexus-monitoring.json");
            }))
        .RequireRateLimiting(DiagnosticsModule.ExportRateLimit).WithName("ExportMonitoringSnapshot");

    public async Task<Result<MonitoringSnapshot>> HandleAsync(Guid user, int window, CancellationToken ct)
    {
        if (!MonitoringReads.ValidWindow(window)) return MonitoringErrors.InvalidWindow;
        var snapshot = snapshots.Read(window);
        await MonitoringReads.AuditAsync(db, user, "monitoring.export", ct);
        return snapshot;
    }
}
