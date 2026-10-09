namespace AiNexus.Features.Monitoring;

public sealed record TrafficMetrics(long Requests, long Errors, long ServerErrors, long Cancelled, long ReceivedBytes, long SentBytes,
    double RequestsPerSecond, double ErrorPercent, double? AverageMs, double? P95Ms);

public sealed record TrafficPoint(DateTimeOffset At, double RequestsPerSecond, double ErrorPercent, double? AverageMs, long ReceivedBytes, long SentBytes);

public sealed record OnlineSession(string Id, Guid UserId, string Account, string DisplayName, string Authentication,
    string Address, string Browser, string Device, string Feature, string State, DateTimeOffset ConnectedAt, DateTimeOffset LastSeenAt,
    string? LastAction, DateTimeOffset? LastActionAt, long Requests, long Errors, long ReceivedBytes, long SentBytes, bool TestingIdentity);

public sealed record TrafficActivity(long Sequence, DateTimeOffset At, Guid? UserId, string? SessionId, string DisplayName,
    string Feature, string Action, string Method, string Route, int StatusCode, double DurationMs, string Outcome, string? TraceId);

public sealed record DependencyTraffic(string Id, string Name, string Kind, string Status, int InFlight, DateTimeOffset? LastSeenAt, TrafficMetrics Metrics);

public sealed record EndpointTraffic(string Method, string Route, string Feature, TrafficMetrics Metrics);

public sealed record RuntimeResources(double? CpuPercent, long WorkingSetBytes, long ManagedHeapBytes, int ThreadCount, double UptimeSeconds);

public sealed record MonitoringSnapshot(int Version, bool Enabled, string Instance, DateTimeOffset StartedAt, DateTimeOffset At, int WindowMinutes,
    int RefreshSeconds, int SessionTimeoutSeconds, int OnlineUsers, int OnlineSessions, int ActiveSessions, int InFlightRequests, int OpenStreams,
    int OmittedSessions, long DroppedSessions, RuntimeResources Resources, TrafficMetrics Traffic, TrafficPoint[] Timeline,
    OnlineSession[] Sessions, TrafficActivity[] Activities, DependencyTraffic[] Dependencies, EndpointTraffic[] Endpoints);
