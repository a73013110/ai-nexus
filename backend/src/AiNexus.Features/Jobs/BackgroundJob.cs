using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Jobs;

[Comment("文件索引與評測等背景工作的租約、進度、重試與取消狀態。")]
public sealed class BackgroundJob
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("W3C 流程追蹤識別，僅由伺服器建立。")]
    public string? TraceId { get; set; }
    [Comment("排程來源的 W3C span 識別，重試沿用同一 trace。")]
    public string? ParentSpanId { get; set; }
    [Comment("持久作業識別，跨佇列與重試保持不變。")]
    public Guid OperationId { get; set; }
    [Comment("工作處理的業務資源識別碼。")]
    public Guid? ResourceId { get; set; }
    [Comment("操作所關聯的業務對象識別碼。")]
    public Guid SubjectId { get; set; }
    [Comment("背景工作種類。")]
    public string Kind { get; set; } = "";
    [Comment("分類標籤或階段的顯示文字。")]
    public string Label { get; set; } = "";
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = "queued";
    [Comment("背景工作目前階段。")]
    public string Stage { get; set; } = "等待處理";
    [Comment("仍在執行工作的唯一鍵，避免同一業務重複排程。")]
    public string? ActiveKey { get; set; }
    [Comment("背景工作租約的 fencing token，防止過期 worker 提交。")]
    public Guid? LeaseToken { get; set; }
    [Comment("背景工作租約的到期時間。")]
    public DateTimeOffset? LeaseUntil { get; set; }
    [Comment("是否收到取消要求；不表示工作已停止。")]
    public bool CancelRequested { get; set; }
    [Comment("背景工作執行／重試次數。")]
    public int Attempt { get; set; }
    [Comment("已完成的真實工作單位數。")]
    public int CompletedUnits { get; set; }
    [Comment("已知的總工作單位數；未知不表示百分比。")]
    public int? TotalUnits { get; set; }
    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }
    [Comment("對外安全的錯誤代碼，不含密碼或完整例外。")]
    public string? ErrorCode { get; set; }
    [Comment("固定安全提示與查證代碼；不可保存例外自由文字。")]
    public string? ErrorMessage { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
    [Comment("資料最後修改時間，採 UTC offset。")]
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> job)
    {
        job.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        job.Property(x => x.UpdatedAt).HasValueGenerator<CreationTime>();
        job.ToTable("BackgroundJobs", "jobs"); job.HasKey(x => x.Id);
        job.Property(x => x.Kind).HasMaxLength(32); job.Property(x => x.Label).HasMaxLength(180); job.Property(x => x.Status).HasMaxLength(16); job.Property(x => x.Stage).HasMaxLength(120);
        job.Property(x => x.IssueCode).HasMaxLength(40); job.Property(x => x.TraceId).HasMaxLength(32); job.Property(x => x.ParentSpanId).HasMaxLength(16);
        job.Property(x => x.ActiveKey).HasMaxLength(100); job.Property(x => x.ErrorCode).HasMaxLength(80); job.Property(x => x.ErrorMessage).HasMaxLength(240);
        job.HasIndex(x => x.ActiveKey).IsUnique().HasFilter("[ActiveKey] IS NOT NULL"); job.HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt }); job.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        job.HasIndex(x => x.SubjectId); // Active work on a document or embedding profile.
    }
}
