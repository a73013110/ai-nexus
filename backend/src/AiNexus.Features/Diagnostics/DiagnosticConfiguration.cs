using AiNexus.Features.AccessControl;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Diagnostics;

public static class DiagnosticConfiguration
{
    public const string Query = "logs.query", Detail = "logs.detail", Export = "logs.export";
    public const string QueryPolicy = Policies.Prefix + Query, DetailPolicy = Policies.Prefix + Detail, ExportPolicy = Policies.Prefix + Export;
    /// <summary>The module's table and features, applied by <c>NexusDbContext</c>.</summary>
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new DiagnosticEventConfiguration());
        PlatformFeatures.Add(model, Query, "系統日誌", "/admin/logs", 110, administratorsOnly: true);
        PlatformFeatures.Add(model, Detail, "日誌診斷詳情", "", 111, administratorsOnly: true);
        PlatformFeatures.Add(model, Export, "日誌匯出", "", 112, administratorsOnly: true);
    }
}

internal sealed class DiagnosticEventConfiguration : IEntityTypeConfiguration<DiagnosticEvent>
{
    public void Configure(EntityTypeBuilder<DiagnosticEvent> log)
    {
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
    }
}
