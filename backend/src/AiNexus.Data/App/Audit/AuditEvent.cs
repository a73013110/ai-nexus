using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Audit;

public sealed class AuditEvent
{
    // AuditEventInterceptor 依這兩個上限截斷，否則超長的值會讓整筆 SaveChanges 失敗。
    public const int ActionMaxLength = 64;
    public const int ResultMaxLength = 80;

    public long Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ActorId { get; set; }
    public string? TraceId { get; set; }
    public Guid? OperationId { get; set; }
    public string? IssueCode { get; set; }
    public string Action { get; set; } = "";
    public Guid? ResourceId { get; set; }
    // 新寫入的業務事件一律帶明確結果；資料庫中既有的 null 保持 null，不回填。
    public string? Result { get; set; } = "completed";
    public string? DetailsJson { get; set; }
    public DateTimeOffset At { get; set; }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> audit)
    {
        audit.Property(x => x.At).HasValueGenerator<CreationTime>();
        audit.ToTable("AuditEvents", "audit");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(AuditEvent.ActionMaxLength);
        audit.Property(x => x.TraceId).HasMaxLength(32);
        audit.Property(x => x.IssueCode).HasMaxLength(40);
        audit.Property(x => x.Result).HasMaxLength(AuditEvent.ResultMaxLength);
        audit.Property(x => x.DetailsJson).HasMaxLength(40000);
    }
}
