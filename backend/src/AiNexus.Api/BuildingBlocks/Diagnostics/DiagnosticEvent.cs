using Microsoft.EntityFrameworkCore;
using AiNexus.Modules.AccessControl;

namespace AiNexus.BuildingBlocks.Diagnostics;

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

public static class DiagnosticConfiguration
{
    public const string Query = "logs.query", Detail = "logs.detail", Export = "logs.export";
    public static void Configure(ModelBuilder model)
    {
        var log = model.Entity<DiagnosticEvent>();
        log.ToTable("DiagnosticEvents", "operations"); log.HasKey(x => x.LogId).IsClustered(false);
        log.Property(x => x.Category).HasMaxLength(180); log.Property(x => x.EventName).HasMaxLength(100);
        log.Property(x => x.MessageTemplate).HasMaxLength(2048); log.Property(x => x.PropertiesJson).HasMaxLength(8192);
        log.Property(x => x.Service).HasMaxLength(80); log.Property(x => x.Environment).HasMaxLength(32);
        log.Property(x => x.Version).HasMaxLength(80); log.Property(x => x.Instance).HasMaxLength(100);
        log.Property(x => x.IssueCode).HasMaxLength(40); log.Property(x => x.TraceId).HasMaxLength(32);
        log.Property(x => x.SpanId).HasMaxLength(16); log.Property(x => x.RequestId).HasMaxLength(40);
        log.Property(x => x.Method).HasMaxLength(10); log.Property(x => x.Route).HasMaxLength(240);
        log.Property(x => x.ExternalService).HasMaxLength(32); log.Property(x => x.ErrorCode).HasMaxLength(80);
        log.Property(x => x.ExceptionType).HasMaxLength(180); log.Property(x => x.ExceptionDetail).HasMaxLength(12000);
        log.HasIndex(x => new { x.At, x.LogId }).IsDescending(true, true).IsClustered();
        log.HasIndex(x => new { x.Level, x.At, x.LogId });
        log.HasIndex(x => new { x.Category, x.At, x.LogId });
        log.HasIndex(x => new { x.EventId, x.At, x.LogId });
        log.HasIndex(x => new { x.EventName, x.At, x.LogId });
        foreach (var field in new[] { nameof(DiagnosticEvent.IssueCode), nameof(DiagnosticEvent.TraceId), nameof(DiagnosticEvent.JobId), nameof(DiagnosticEvent.RunId), nameof(DiagnosticEvent.OperationId), nameof(DiagnosticEvent.ErrorCode), nameof(DiagnosticEvent.Instance) })
            log.HasIndex(field, nameof(DiagnosticEvent.At), nameof(DiagnosticEvent.LogId)).HasFilter("[" + field + "] IS NOT NULL");
        PlatformFeatures.Add(model, Query, "系統日誌", "/admin/logs", 110, administratorsOnly: true);
        PlatformFeatures.Add(model, Detail, "日誌診斷詳情", "", 111, administratorsOnly: true);
        PlatformFeatures.Add(model, Export, "日誌匯出", "", 112, administratorsOnly: true);
    }
}
