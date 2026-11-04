using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>本實例最近 1、5 或 15 分鐘的即時工作階段、流量與相依服務；讀取會被稽核。</summary>
internal sealed class GetMonitoringSnapshot(MonitoringSnapshots snapshots) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder monitor) => monitor
        .MapGet("", (int? minutes, GetMonitoringSnapshot handler) => handler.Handle(minutes ?? MonitoringReads.DefaultWindow))
        .Audited(MonitoringAudit.SnapshotRead)
        .WithName("GetMonitoringSnapshot");

    public Result<MonitoringSnapshot> Handle(int window) => MonitoringReads.Read(snapshots, window);
}
