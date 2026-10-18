using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Diagnostics;

/// <summary>
/// <c>diagnostics.DiagnosticEvents</c> 的對應。實體是 Platform 日誌管線本身的 <see cref="DiagnosticEvent"/>：
/// 管線在任何模組載入前就要能緩衝與寫檔，所以型別留在 Platform；這是唯一只放組態的表，檔名因此以組態命名。
/// </summary>
internal sealed class DiagnosticEventConfiguration : IEntityTypeConfiguration<DiagnosticEvent>
{
    public void Configure(EntityTypeBuilder<DiagnosticEvent> log)
    {
        log.ToTable("DiagnosticEvents", "diagnostics");
        log.HasKey(x => x.LogId);
        log.Property(x => x.Category).HasMaxLength(180);
        log.Property(x => x.EventName).HasMaxLength(100);
        log.Property(x => x.MessageTemplate).HasMaxLength(2048);
        log.Property(x => x.PropertiesJson).HasMaxLength(8192);
        log.Property(x => x.Service).HasMaxLength(80);
        log.Property(x => x.Environment).HasMaxLength(32);
        log.Property(x => x.Version).HasMaxLength(80);
        log.Property(x => x.Instance).HasMaxLength(100);
        log.Property(x => x.IssueCode).HasMaxLength(40);
        log.Property(x => x.TraceId).HasMaxLength(32);
        log.Property(x => x.SpanId).HasMaxLength(16);
        log.Property(x => x.RequestId).HasMaxLength(40);
        log.Property(x => x.Method).HasMaxLength(10);
        log.Property(x => x.Route).HasMaxLength(240);
        log.Property(x => x.ExternalService).HasMaxLength(32);
        log.Property(x => x.ErrorCode).HasMaxLength(80);
        log.Property(x => x.ExceptionType).HasMaxLength(180);
        log.Property(x => x.ExceptionDetail).HasMaxLength(12000);
    }
}
