namespace AiNexus.Platform.Diagnostics;

public sealed class DiagnosticEvent
{
    public Guid LogId { get; set; } = Guid.NewGuid();
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public LogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string MessageTemplate { get; set; } = "";
    public string PropertiesJson { get; set; } = "{}";
    public string Service { get; set; } = "AiNexus";
    public string Environment { get; set; } = "";
    public string Version { get; set; } = "";
    public string Instance { get; set; } = "";
    public string? IssueCode { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? RequestId { get; set; }
    public Guid? OperationId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? RunId { get; set; }
    public Guid? UserId { get; set; }
    public int? Attempt { get; set; }
    public string? Method { get; set; }
    public string? Route { get; set; }
    public int? StatusCode { get; set; }
    public double? DurationMs { get; set; }
    public string? ExternalService { get; set; }
    public string? ErrorCode { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionDetail { get; set; }
    public bool UntrustedClient { get; set; }
}
