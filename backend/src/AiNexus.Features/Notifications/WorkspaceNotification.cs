using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Notifications;

// Payload version and typed targets are independent of delivery channels and UI URLs.
[Comment("使用者個人通知、版本化導向、閱讀狀態與事件去重鍵。")]
public sealed class WorkspaceNotification
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("業務版本號，用於歷史或樂觀並行控制。")]
    public int Version { get; set; } = 1;
    [Comment("通知來源事件的冪等識別碼。")]
    public string EventKey { get; set; } = "";
    [Comment("事件的種類。")]
    public string Type { get; set; } = "";
    [Comment("通知呈現層級：info、success 或 error。")]
    public string Severity { get; set; } = "info";
    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "";
    [Comment("通知摘要，不包含完整私密原文。")]
    public string Body { get; set; } = "";
    [Comment("已核准的功能導向類型。")]
    public string TargetKind { get; set; } = "";
    [Comment("通知所指向的業務識別碼。")]
    public Guid TargetId { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
    [Comment("通知已閱讀的時間；空值代表未讀。")]
    public DateTimeOffset? ReadAt { get; set; }
    [Comment("通知移除的時間；空值代表仍可查看。")]
    public DateTimeOffset? DismissedAt { get; set; }
}

internal static class NotificationQueries
{
    /// <summary>The owner's notifications that are still in the inbox.</summary>
    public static IQueryable<WorkspaceNotification> Inbox(this IQueryable<WorkspaceNotification> notifications, Guid owner)
        => notifications.Where(x => x.OwnerId == owner && x.DismissedAt == null);
}

internal sealed class WorkspaceNotificationConfiguration : IEntityTypeConfiguration<WorkspaceNotification>
{
    public void Configure(EntityTypeBuilder<WorkspaceNotification> n)
    {
        n.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        n.ToTable("Notifications", "notifications"); n.HasKey(x => x.Id);
        n.Property(x => x.IssueCode).HasMaxLength(40); n.Property(x => x.EventKey).HasMaxLength(160); n.Property(x => x.Type).HasMaxLength(80); n.Property(x => x.Severity).HasMaxLength(16);
        n.Property(x => x.Title).HasMaxLength(180); n.Property(x => x.Body).HasMaxLength(600); n.Property(x => x.TargetKind).HasMaxLength(32);
        n.HasIndex(x => new { x.OwnerId, x.EventKey }).IsUnique(); n.HasIndex(x => new { x.OwnerId, x.DismissedAt, x.ReadAt, x.CreatedAt });
    }
}
