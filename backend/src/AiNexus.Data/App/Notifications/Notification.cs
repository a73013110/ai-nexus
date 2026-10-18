using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Notifications;

// 通知只存類型與業務 ID，不存前端 URL 或投遞管道；改版路由時，已存下的通知不必遷移。
public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public int Version { get; set; } = 1;
    public string EventKey { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string? IssueCode { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string TargetKind { get; set; } = "";
    public Guid TargetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> n)
    {
        n.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        n.ToTable("Notifications", "notifications");
        n.HasKey(x => x.Id);
        n.Property(x => x.IssueCode).HasMaxLength(40);
        n.Property(x => x.EventKey).HasMaxLength(160);
        n.Property(x => x.Type).HasMaxLength(80);
        n.Property(x => x.Severity).HasMaxLength(16);
        n.Property(x => x.Title).HasMaxLength(180);
        n.Property(x => x.Body).HasMaxLength(600);
        n.Property(x => x.TargetKind).HasMaxLength(32);
        n.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
