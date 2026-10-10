using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Persistence;

public static class AuditOutcomes
{
    // Legacy records retain their original result; filters must recognize every successful operation.
    public static readonly string[] Accepted = ["saved", "read", "completed", "success", "granted", "granted_once", "created", "deleted", "soft_deleted", "queued", "running", "cancelled", "revoked", "removed", "assigned", "read-only", "private", "started", "restored", "activated", "cleared", "connected", "csv", "metrics", "reindexing", "snapshot"];
}

[Comment("操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。")]
public sealed class AuditEvent
{
    [Comment("資料的主鍵識別碼。")]
    public long Id { get; set; }
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。")]
    public Guid? ActorId { get; set; }
    [Comment("W3C 流程追蹤識別，僅由伺服器建立。")]
    public string? TraceId { get; set; }
    [Comment("持久作業識別，跨佇列與重試保持不變。")]
    public Guid? OperationId { get; set; }
    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }
    [Comment("稽核操作名稱。")]
    public string Action { get; set; } = "";
    [Comment("稽核對象的業務資源識別碼。")]
    public Guid? ResourceId { get; set; }
    // New committed business events have an explicit outcome; stored legacy nulls remain null.
    [Comment("稽核操作結果或失敗代碼。")]
    public string? Result { get; set; } = "completed";
    [Comment("稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。")]
    public string? DetailsJson { get; set; }
    [Comment("稽核事件發生時間。")]
    public DateTimeOffset At { get; set; }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> audit)
    {
        audit.Property(x => x.At).HasValueGenerator<CreationTime>();
        audit.ToTable("AuditEvents", "audit");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(64);
        audit.Property(x => x.TraceId).HasMaxLength(32); audit.Property(x => x.IssueCode).HasMaxLength(40);
        audit.Property(x => x.Result).HasMaxLength(80);
        audit.Property(x => x.DetailsJson).HasMaxLength(40000);
        audit.HasIndex(x => x.At);
        audit.HasIndex(x => new { x.Action, x.Id });
        audit.HasIndex(x => new { x.ResourceId, x.Id });
        audit.HasIndex(x => new { x.ActorId, x.Id });
    }
}
