using System.Globalization;
using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>
/// 把快照轉成頁面上的健康判定，是純函式，只依門檻運算。
/// 流量規則讀 <see cref="MonitoringSnapshot.RecentTraffic"/>（最後一分鐘）；
/// 資源規則讀最後 <see cref="SustainedPoints"/> 個資源區間；相依服務讀使用者所選的時間窗。
/// 門檻值的說明見 docs/features/monitoring.md。
/// </summary>
public static class MonitoringHealth
{
    public const string Healthy = "healthy", Attention = "attention", Critical = "critical", Unobserved = "unobserved";
    public const string Warning = "warning";

    /// <summary>最近一分鐘請求數未達此值時，錯誤率與延遲規則都不判定，避免樣本太少而誤報。</summary>
    public const int MinimumRequests = 20;
    public const double ErrorWarningPercent = 5, ErrorCriticalPercent = 25;
    public const double LatencyWarningMs = 3000;
    /// <summary>相依服務的呼叫全部失敗時：呼叫數達此值為嚴重，否則為警告。</summary>
    public const int DependencyCriticalCalls = 3;
    /// <summary>資源規則需要連續這麼多個十秒區間都超過門檻，才會判定，以濾掉單次尖峰。</summary>
    public const int SustainedPoints = 3;
    public const double CpuWarningPercent = 85, GcPauseWarningPercent = 10, MemoryWarningPercent = 90;
    public const long ThreadPoolQueueWarning = 100;

    /// <summary>尚未執行 <see cref="Evaluate"/> 前的佔位值。</summary>
    public static readonly HealthSummary Pending = new(Unobserved, []);

    public static HealthSummary Evaluate(MonitoringSnapshot snapshot)
    {
        if (!snapshot.Enabled) return new(Unobserved, []);
        var issues = new List<HealthIssue>();
        Traffic(snapshot.RecentTraffic, issues);
        Resources(snapshot, issues);
        foreach (var dependency in snapshot.Dependencies) Dependency(dependency, snapshot.WindowMinutes, issues);
        foreach (var subsystem in snapshot.Subsystems) Subsystem(subsystem, issues);
        if (snapshot.DroppedSessions > 0)
            issues.Add(new("sessions_dropped", Warning, "sessions", "工作階段已達上限",
                $"已有 {N(snapshot.DroppedSessions)} 個新工作階段因容量上限未被記錄。"));
        var ordered = issues.OrderBy(x => x.Severity == Critical ? 0 : 1).ToArray();
        if (ordered.Length > 0) return new(ordered[0].Severity == Critical ? Critical : Attention, ordered);
        return new(Observed(snapshot) ? Healthy : Unobserved, []);
    }

    private static void Traffic(TrafficMetrics recent, List<HealthIssue> issues)
    {
        if (recent.Requests >= MinimumRequests && recent.ErrorPercent > ErrorWarningPercent)
            issues.Add(new("error_rate_high", recent.ErrorPercent > ErrorCriticalPercent ? Critical : Warning, "api", "API 錯誤率偏高",
                $"最近 1 分鐘 {N(recent.Requests)} 次要求中有 {N(recent.Errors)} 次錯誤（{P(recent.ErrorPercent)}）。"));
        if (recent.ServerErrors > 0)
            issues.Add(new("server_errors", Warning, "api", "發生伺服器錯誤", $"最近 1 分鐘有 {N(recent.ServerErrors)} 次 5xx 回應。"));
        // 有計時過的請求卻沒有 P95，代表落在直方圖最後一個上界之外。
        var slow = recent.P95Ms > LatencyWarningMs || recent is { P95Ms: null, AverageMs: not null };
        if (recent.Requests >= MinimumRequests && slow)
            issues.Add(new("latency_high", Warning, "api", "回應變慢", recent.P95Ms is { } bound
                ? $"最近 1 分鐘的 P95 回應時間達 {Ms(bound)}，超過 {Ms(LatencyWarningMs)}。"
                : "最近 1 分鐘的 P95 回應時間超過 600 秒。"));
    }

    private static void Resources(MonitoringSnapshot snapshot, List<HealthIssue> issues)
    {
        var last = snapshot.ResourceTimeline.TakeLast(SustainedPoints).ToArray();
        var full = last.Length == SustainedPoints;
        if (full && last.All(x => x.CpuPercent > CpuWarningPercent))
            issues.Add(new("cpu_high", Warning, "cpu", "CPU 使用率偏高",
                $"最近 {SustainedPoints * RuntimeTraffic.BucketSeconds} 秒 CPU 平均 {P(last.Average(x => x.CpuPercent!.Value))}，超過 {P(CpuWarningPercent)}。"));
        if (full && last.All(x => x.ThreadPoolQueueLength > ThreadPoolQueueWarning))
            issues.Add(new("thread_pool_starvation", Warning, "thread_pool", "執行緒集區排隊",
                $"最近 {SustainedPoints * RuntimeTraffic.BucketSeconds} 秒待處理工作持續超過 {N(ThreadPoolQueueWarning)} 項，最多 {N(last.Max(x => x.ThreadPoolQueueLength!.Value))} 項。"));
        if (full && last.All(x => x.GcPausePercent > GcPauseWarningPercent))
            issues.Add(new("gc_pause_high", Warning, "gc", "GC 暫停過長",
                $"最近 {SustainedPoints * RuntimeTraffic.BucketSeconds} 秒有 {P(last.Average(x => x.GcPausePercent!.Value))} 的時間暫停於 GC。"));
        var resources = snapshot.Resources;
        if (resources.MemoryLimitBytes is > 0 and var limit && resources.WorkingSetBytes * 100d / limit > MemoryWarningPercent)
            issues.Add(new("memory_high", Warning, "memory", "記憶體接近上限",
                $"工作集 {Bytes(resources.WorkingSetBytes)} 已達可用記憶體 {Bytes(limit)} 的 {P(resources.WorkingSetBytes * 100d / limit)}。"));
    }

    private static void Dependency(DependencyTraffic dependency, int window, List<HealthIssue> issues)
    {
        var metrics = dependency.Metrics;
        if (dependency.Status == "failing")
            issues.Add(new("dependency_failing", metrics.Requests >= DependencyCriticalCalls ? Critical : Warning, dependency.Id, dependency.Name + "無法使用",
                $"最近 {window} 分鐘 {N(metrics.Requests)} 次呼叫全部失敗。"));
        else if (dependency.Status == "degraded")
            issues.Add(new("dependency_degraded", Warning, dependency.Id, dependency.Name + "部分失敗",
                $"最近 {window} 分鐘 {N(metrics.Requests)} 次呼叫中有 {N(metrics.Errors)} 次失敗（{P(metrics.ErrorPercent)}）。"));
    }

    private static void Subsystem(MonitoredSubsystem subsystem, List<HealthIssue> issues)
    {
        if (subsystem.Status == SubsystemStates.Failing)
            issues.Add(new("subsystem_failing", Critical, subsystem.Id, subsystem.Name + "異常", subsystem.Summary ?? subsystem.Name + "目前無法運作。"));
        else if (subsystem.Status == SubsystemStates.Degraded)
            issues.Add(new("subsystem_degraded", Warning, subsystem.Id, subsystem.Name + "需要注意", subsystem.Summary ?? subsystem.Name + "部分功能受影響。"));
        // 來源未被使用時回報 idle；只有讀取過卻仍為 unobserved，才代表讀取本身失敗或逾時。
        else if (subsystem is { Status: SubsystemStates.Unobserved, ObservedAt: not null })
            issues.Add(new("subsystem_unreadable", Warning, subsystem.Id, subsystem.Name + "無法讀取", $"最近一次讀取{subsystem.Name}狀態失敗或逾時。"));
    }

    /// <summary>是否量測到任何資料：完成的請求、程序取樣、相依服務呼叫或子系統讀數。</summary>
    private static bool Observed(MonitoringSnapshot snapshot) =>
        snapshot.Traffic.Requests > 0 || snapshot.RecentTraffic.Requests > 0 || snapshot.Resources.WorkingSetBytes > 0
        || snapshot.Dependencies.Any(x => x.Status != "unobserved") || snapshot.Subsystems.Any(x => x.ObservedAt is not null && x.Status != SubsystemStates.Unobserved);

    private static string N(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
    private static string P(double value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    private static string Ms(double value) => value >= 1000 ? (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + " 秒" : N(value) + " 毫秒";
    private static string Bytes(long value) => value >= 1L << 30
        ? (value / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB"
        : (value / (double)(1L << 20)).ToString("0", CultureInfo.InvariantCulture) + " MB";
}
