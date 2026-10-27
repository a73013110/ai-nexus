using System.Text.Json;
using AiNexus.Features.Admin.Diagnostics;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>以 JSON 檔案下載即時監控快照，受匯出速率限制；釋出前會先寫入稽核紀錄。</summary>
internal sealed class ExportMonitoringSnapshot(MonitoringSnapshots snapshots)
{
    public static void Map(RouteGroupBuilder monitor) => monitor
        .MapGet("/export", (int? minutes, ICurrentUser user, HttpContext http, ExportMonitoringSnapshot handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, minutes ?? MonitoringReads.DefaultWindow, ct).ToHttpResultAsync(snapshot =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web), "application/json", "ai-nexus-monitoring.json");
            }))
        .RequireRateLimiting(DiagnosticsModule.ExportRateLimit).Audited(MonitoringAudit.SnapshotExported).WithName("ExportMonitoringSnapshot");

    public Task<Result<MonitoringSnapshot>> HandleAsync(Guid user, int window, CancellationToken ct)
        => Task.FromResult(MonitoringReads.Read(snapshots, window));
}
