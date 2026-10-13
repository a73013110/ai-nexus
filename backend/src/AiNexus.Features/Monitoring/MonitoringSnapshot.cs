using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Monitoring;

public sealed record TrafficMetrics(long Requests, long Errors, long ServerErrors, long Cancelled, long ReceivedBytes, long SentBytes,
    double RequestsPerSecond, double ErrorPercent, double? AverageMs, double? P95Ms);

/// <summary>One completed ten-second bucket. <c>P95Ms</c> is a histogram bound, like <see cref="TrafficMetrics.P95Ms"/>.</summary>
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
/// The latest three-second process sample. GC counts are since process start; <c>GcPausePercent</c> and
/// <c>LockContentionsPerSecond</c> cover the last sample interval. <c>MemoryLimitBytes</c> is the memory the GC may use
/// (physical memory, container or hard limit), null when unknown.
/// </summary>
public sealed record RuntimeResources(double? CpuPercent, long WorkingSetBytes, long ManagedHeapBytes, long? MemoryLimitBytes, int ThreadCount,
    int ThreadPoolThreads, long ThreadPoolQueueLength, double LockContentionsPerSecond, int Gen0Collections, int Gen1Collections, int Gen2Collections,
    double? GcPausePercent, double UptimeSeconds);

/// <summary>
/// Process resources of one ten-second bucket, index-aligned with <see cref="MonitoringSnapshot.Timeline"/>: average CPU
/// and GC pause, the last memory figures and the longest thread-pool queue of its samples. All null when no sample fell in it.
/// </summary>
public sealed record ResourcePoint(DateTimeOffset At, double? CpuPercent, long? WorkingSetBytes, long? ManagedHeapBytes, long? ThreadPoolQueueLength, double? GcPausePercent);

/// <summary>A subsystem reported by another module's <see cref="IMonitoringSource"/>; <c>ObservedAt</c> is null until its first read.</summary>
public sealed record MonitoredSubsystem(string Id, string Name, string Status, string? Summary, SubsystemFact[] Facts, DateTimeOffset? ObservedAt);

/// <summary>A problem worth an administrator's attention; <c>Subject</c> is the dependency, subsystem or metric id it concerns.</summary>
public sealed record HealthIssue(string Code, string Severity, string Subject, string Title, string Detail);

/// <summary>Overall verdict (<c>healthy</c>, <c>attention</c>, <c>critical</c> or <c>unobserved</c>) with its issues, critical first.</summary>
public sealed record HealthSummary(string Status, HealthIssue[] Issues);

/// <summary>
/// Live view of this instance. <c>Traffic</c>, dependencies, endpoints and activities follow <c>WindowMinutes</c>;
/// <c>RecentTraffic</c> is always the last minute of completed buckets and drives <c>Health</c>.
/// </summary>
public sealed record MonitoringSnapshot(int Version, bool Enabled, string Instance, DateTimeOffset StartedAt, DateTimeOffset At, int WindowMinutes,
    int RefreshSeconds, int SessionTimeoutSeconds, HealthSummary Health, int OnlineUsers, int OnlineSessions, int ActiveSessions, int InFlightRequests,
    int OpenStreams, int OmittedSessions, long DroppedSessions, RuntimeResources Resources, ResourcePoint[] ResourceTimeline, TrafficMetrics Traffic,
    TrafficMetrics RecentTraffic, TrafficPoint[] Timeline, OnlineSession[] Sessions, TrafficActivity[] Activities, DependencyTraffic[] Dependencies,
    EndpointTraffic[] Endpoints, MonitoredSubsystem[] Subsystems)
{
    /// <summary>Contract version; the page refuses snapshots it does not understand.</summary>
    public const int CurrentVersion = 2;
}
