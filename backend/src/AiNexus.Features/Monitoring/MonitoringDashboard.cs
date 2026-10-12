using AiNexus.Platform.Dashboard;

namespace AiNexus.Features.Monitoring;

/// <summary>
/// Live load of this instance over the last five minutes: people online, requests and errors, and dependency health.
/// Only when monitoring is enabled, and only for the whole platform.
/// </summary>
internal sealed class MonitoringDashboard(RuntimeTraffic traffic) : IDashboardSource
{
    private const int WindowMinutes = 5;

    public string Feature => MonitoringModule.Feature;

    public bool PlatformOnly => true;

    public Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        if (!traffic.Enabled || scope.Owner is not null) return Task.FromResult<DashboardSectionDto?>(null);
        var snapshot = traffic.Snapshot(WindowMinutes);
        var errors = snapshot.Traffic.ErrorPercent;
        var section = new DashboardSection("monitoring", Feature)
            .Count("online", "在線人數", snapshot.OnlineUsers, "人", snapshot.OnlineUsers > 0 ? DashboardTones.Info : DashboardTones.Neutral,
                $"{snapshot.OnlineSessions} 個工作階段 · {snapshot.ActiveSessions} 個活躍")
            .Count("rps", "每秒請求", Math.Round(snapshot.Traffic.RequestsPerSecond, 2), "次", hint: $"近 {WindowMinutes} 分鐘")
            .Metric(new("errors", "錯誤比例", errors, "", DashboardFormats.Percent, errors >= 5 ? DashboardTones.Danger : errors >= 1 ? DashboardTones.Warning : DashboardTones.Neutral,
                false, $"近 {WindowMinutes} 分鐘 · {snapshot.Traffic.ServerErrors} 次伺服器錯誤"))
            .Metric(new("p95", "P95 回應時間", snapshot.Traffic.P95Ms, "", DashboardFormats.Duration, DashboardTones.Neutral, false, $"近 {WindowMinutes} 分鐘"))
            .Count("streams", "開啟中的串流", snapshot.OpenStreams, "條")
            .Size("memory", "記憶體", snapshot.Resources.WorkingSetBytes, snapshot.Resources.CpuPercent is double cpu ? $"CPU {cpu:0.#}%" : null)
            .Fact("執行個體", snapshot.Instance)
            .Fact("已執行", Uptime(snapshot.Resources.UptimeSeconds))
            .ActiveWhen(snapshot.InFlightRequests);
        foreach (var dependency in snapshot.Dependencies)
            section.Fact(dependency.Name, dependency.Status switch { "healthy" => "正常", "degraded" => "部分失敗", "failing" => "全部失敗", _ => "尚無流量" },
                dependency.Status switch { "healthy" => DashboardTones.Success, "degraded" => DashboardTones.Warning, "failing" => DashboardTones.Danger, _ => DashboardTones.Neutral });
        return Task.FromResult<DashboardSectionDto?>(section.Build());
    }

    private static string Uptime(double seconds) => TimeSpan.FromSeconds(seconds) switch
    {
        { TotalDays: >= 1 } span => $"{(int)span.TotalDays} 天 {span.Hours} 小時",
        { TotalHours: >= 1 } span => $"{(int)span.TotalHours} 小時 {span.Minutes} 分",
        var span => $"{span.Minutes} 分",
    };
}
