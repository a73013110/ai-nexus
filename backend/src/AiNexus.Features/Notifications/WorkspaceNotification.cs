using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Notifications;

// Payload version and typed targets are independent of delivery channels and UI URLs.
public sealed class WorkspaceNotification
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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
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
        n.ToTable("Notifications", "operations"); n.HasKey(x => x.Id);
        n.Property(x => x.IssueCode).HasMaxLength(40); n.Property(x => x.EventKey).HasMaxLength(160); n.Property(x => x.Type).HasMaxLength(80); n.Property(x => x.Severity).HasMaxLength(16);
        n.Property(x => x.Title).HasMaxLength(180); n.Property(x => x.Body).HasMaxLength(600); n.Property(x => x.TargetKind).HasMaxLength(32);
        n.HasIndex(x => new { x.OwnerId, x.EventKey }).IsUnique(); n.HasIndex(x => new { x.OwnerId, x.DismissedAt, x.ReadAt, x.CreatedAt });
    }
}
