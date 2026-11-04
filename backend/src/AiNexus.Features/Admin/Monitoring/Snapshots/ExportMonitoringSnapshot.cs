using System.Text.Json;
using AiNexus.Features.Admin.Diagnostics;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>以 JSON 檔案下載即時監控快照，受匯出速率限制；釋出前會先寫入稽核紀錄。</summary>
internal sealed class ExportMonitoringSnapshot(MonitoringSnapshots snapshots) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder monitor) => monitor
        .MapGet("/export", (int? minutes, ExportMonitoringSnapshot handler) =>
            handler.Handle(minutes ?? MonitoringReads.DefaultWindow).ToHttpResult(snapshot =>
                new ExportFile("ai-nexus-monitoring.json", "application/json", JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web)).ToHttpResult()))
        .RequireRateLimiting(DiagnosticsModule.ExportRateLimit).Audited(MonitoringAudit.SnapshotExported).WithName("ExportMonitoringSnapshot");

    public Result<MonitoringSnapshot> Handle(int window) => MonitoringReads.Read(snapshots, window);
}
