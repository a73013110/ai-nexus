namespace AiNexus.Modules.Monitoring;

public sealed class MonitoringOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxSessions { get; set; } = 2000;
    public int SessionTimeoutSeconds { get; set; } = 90;
    public int RefreshSeconds { get; set; } = 3;
    public bool Valid() => MaxSessions is >= 100 and <= 10000 && SessionTimeoutSeconds is >= 60 and <= 300 && RefreshSeconds is >= 2 and <= 15;
}

public sealed record PresenceRequest(Guid SessionId, string Feature, string State);
public sealed record PresenceReceipt(bool Enabled, int HeartbeatSeconds);
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

/// <summary>A fixed vocabulary prevents client URLs, resource titles and query strings entering telemetry.</summary>
public static class MonitoringVocabulary
{
    private static readonly IReadOnlyDictionary<string, string> Features = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dashboard"] = "總覽", ["chat"] = "對話", ["files"] = "檔案庫", ["knowledge"] = "知識庫", ["reader"] = "閱讀器",
        ["projects"] = "專案", ["artifacts"] = "成果文件", ["shared"] = "分享", ["quality"] = "品質評測", ["tasks"] = "背景任務",
        ["repositories"] = "程式庫", ["integrations"] = "資料來源", ["admin"] = "平台管理", ["monitoring"] = "即時監控",
        ["audit"] = "活動稽核", ["logs"] = "系統日誌", ["settings"] = "個人設定"
    };
    public static bool ValidFeature(string feature) => Features.ContainsKey(feature);
    public static string Name(string feature) => Features.GetValueOrDefault(feature, "系統");
    public static string Feature(string route)
    {
        var segments = route.Replace("/api/v1/", "", StringComparison.Ordinal).TrimStart('/').Split('/');
        var segment = segments[0];
        if (segment == "admin" && segments.Length > 1 && segments[1] is "monitoring" or "audit" or "logs") return segments[1];
        return segment switch
        {
            "conversations" or "runs" or "context" or "models" or "prompt-templates" or "tools" => "chat",
            "attachments" => "files", "documents" => "knowledge", "jobs" => "tasks", "shares" => "shared",
            "preferences" or "settings" or "me" => "settings", _ => ValidFeature(segment) ? segment : "system"
        };
    }
    public static string Action(string method, string feature) => method switch
    {
        "POST" => "執行" + Name(feature), "PUT" or "PATCH" => "更新" + Name(feature), "DELETE" => "刪除" + Name(feature),
        _ => "讀取" + Name(feature)
    };
}
