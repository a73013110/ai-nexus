using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

public sealed record TrafficMetrics(long Requests, long Errors, long ServerErrors, long Cancelled, long ReceivedBytes, long SentBytes,
    double RequestsPerSecond, double ErrorPercent, double? AverageMs, double? P95Ms);

/// <summary>一個已完成的十秒區間。<c>P95Ms</c> 是直方圖的上界而非精確百分位，與 <see cref="TrafficMetrics.P95Ms"/> 相同。</summary>
public sealed record TrafficPoint(DateTimeOffset At, double RequestsPerSecond, double ErrorPercent, long ServerErrors, double? AverageMs, double? P95Ms,
    long ReceivedBytes, long SentBytes);

public sealed record OnlineSession(string Id, Guid UserId, string Account, string DisplayName, string Authentication,
    string Address, string Browser, string Device, string Feature, string State, DateTimeOffset ConnectedAt, DateTimeOffset LastSeenAt,
    string? LastAction, DateTimeOffset? LastActionAt, long Requests, long Errors, long ReceivedBytes, long SentBytes, bool TestingIdentity);

public sealed record TrafficActivity(long Sequence, DateTimeOffset At, Guid? UserId, string? SessionId, string DisplayName,
    string Feature, string Action, string Method, string Route, int StatusCode, double DurationMs, string Outcome, string? TraceId);

public sealed record DependencyTraffic(string Id, string Name, string Kind, string Status, int InFlight, DateTimeOffset? LastSeenAt, TrafficMetrics Metrics);

public sealed record EndpointTraffic(string Method, string Route, string Feature, TrafficMetrics Metrics);

/// <summary>
/// 最新一筆三秒程序取樣。GC 次數自程序啟動起累計；<c>GcPausePercent</c> 與 <c>LockContentionsPerSecond</c> 只涵蓋最後一個取樣區間。<c>MemoryLimitBytes</c> 為 GC 可使用的記憶體上限（實體記憶體、容器或硬性上限），未知時為 null。
/// </summary>
public sealed record RuntimeResources(double? CpuPercent, long WorkingSetBytes, long ManagedHeapBytes, long? MemoryLimitBytes, int ThreadCount,
    int ThreadPoolThreads, long ThreadPoolQueueLength, double LockContentionsPerSecond, int Gen0Collections, int Gen1Collections, int Gen2Collections,
    double? GcPausePercent, double UptimeSeconds);

/// <summary>
/// 一個十秒區間的程序資源，與 <see cref="MonitoringSnapshot.ResourceTimeline"/> 以索引對齊：CPU 與 GC 暫停取平均、記憶體取最後值、執行緒集區佇列取最長值；區間內沒有取樣時全部為 null。
/// </summary>
public sealed record ResourcePoint(DateTimeOffset At, double? CpuPercent, long? WorkingSetBytes, long? ManagedHeapBytes, long? ThreadPoolQueueLength, double? GcPausePercent);

/// <summary>其他模組的 <see cref="IMonitoringSource"/> 所回報的子系統；<c>ObservedAt</c> 在第一次讀取前為 null。</summary>
public sealed record MonitoredSubsystem(string Id, string Name, string Status, string? Summary, SubsystemFact[] Facts, DateTimeOffset? ObservedAt);

/// <summary>值得管理員注意的問題；<c>Subject</c> 為相關的相依服務、子系統或指標 id。</summary>
public sealed record HealthIssue(string Code, string Severity, string Subject, string Title, string Detail);

/// <summary>整體判定（<c>healthy</c>、<c>attention</c>、<c>critical</c> 或 <c>unobserved</c>）與問題清單，嚴重者在前。</summary>
public sealed record HealthSummary(string Status, HealthIssue[] Issues);

/// <summary>
/// 本實例的即時檢視。<c>Traffic</c>、相依服務、端點與活動隨 <c>WindowMinutes</c> 變動；<c>RecentTraffic</c> 永遠是最後一分鐘的已完成區間，並作為 <c>Health</c> 的依據。
/// </summary>
public sealed record MonitoringSnapshot(int Version, bool Enabled, string Instance, DateTimeOffset StartedAt, DateTimeOffset At, int WindowMinutes,
    int RefreshSeconds, int SessionTimeoutSeconds, HealthSummary Health, int OnlineUsers, int OnlineSessions, int ActiveSessions, int InFlightRequests,
    int OpenStreams, int OmittedSessions, long DroppedSessions, RuntimeResources Resources, ResourcePoint[] ResourceTimeline, TrafficMetrics Traffic,
    TrafficMetrics RecentTraffic, TrafficPoint[] Timeline, OnlineSession[] Sessions, TrafficActivity[] Activities, DependencyTraffic[] Dependencies,
    EndpointTraffic[] Endpoints, MonitoredSubsystem[] Subsystems)
{
    /// <summary>合約版本；頁面遇到不認得的版本會拒絕顯示。</summary>
    public const int CurrentVersion = 2;
}
