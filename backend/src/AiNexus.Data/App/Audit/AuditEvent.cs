using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Audit;

/// <summary>一筆稽核：誰（OwnerId／ActorId）在哪個功能（FeatureId）對誰的資料（SubjectUserId）做了什麼（Action），結果如何。只經 <see cref="AuditTrail"/> 寫入。</summary>
public sealed class AuditEvent
{
    // AuditEventInterceptor 依這兩個上限截斷，否則超長的值會讓整筆 SaveChanges 失敗。
    public const int ActionMaxLength = AuditAction.NameMaxLength;
    public const int ResultMaxLength = 80;
    public const int CategoryMaxLength = 16;
    public const int FeatureIdMaxLength = 64;
    /// <summary>成功的結果值；失敗時 <see cref="Result"/> 是錯誤代碼（ADR 0023）。</summary>
    public const string Completed = "completed";

    public long Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ActorId { get; set; }
    public string? TraceId { get; set; }
    public Guid? OperationId { get; set; }
    public string? IssueCode { get; set; }
    public string Action { get; set; } = "";
    public string Category { get; set; } = "";
    public string? FeatureId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string Result { get; set; } = Completed;
    public string? DetailsJson { get; set; }
    public string? ClientAddress { get; set; }
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
        audit.Property(x => x.Category).HasMaxLength(AuditEvent.CategoryMaxLength);
        audit.Property(x => x.FeatureId).HasMaxLength(AuditEvent.FeatureIdMaxLength);
        audit.Property(x => x.TraceId).HasMaxLength(32);
        audit.Property(x => x.IssueCode).HasMaxLength(40);
        audit.Property(x => x.Result).HasMaxLength(AuditEvent.ResultMaxLength);
        audit.Property(x => x.DetailsJson).HasMaxLength(40000);
        audit.Property(x => x.ClientAddress).HasMaxLength(AuditContextSource.ClientAddressMaxLength);
    }
}
