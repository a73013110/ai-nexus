using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Mail;

/// <summary>
/// 一封信（一位收件者）的 outbox 列：入列時與業務變更同一交易寫入，由寄送背景工作認領。
/// <c>ResendOfId</c> 刻意不設外鍵：保存清理刪除原信時不受重寄信阻擋。
/// </summary>
public sealed class OutboundMessage
{
    public const int CategoryMaxLength = 64;
    public const int IdempotencyKeyMaxLength = 200;
    public const int AddressMaxLength = 254;
    public const int NameMaxLength = 120;
    public const int SubjectMaxLength = 200;
    public const int ResponseMaxLength = 512;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = "";
    public MailPriority Priority { get; set; } = MailPriority.Normal;
    public string Status { get; set; } = MailStates.Queued;
    public bool Sensitive { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? ResendOfId { get; set; }
    public Guid? RequestedById { get; set; }
    public Guid? RecipientUserId { get; set; }
    public string? RecipientAddress { get; set; }
    public string? RecipientName { get; set; }
    public string RecipientMasked { get; set; } = "";
    public byte[] RecipientHash { get; set; } = [];
    public string FromAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string HeaderMessageId { get; set; } = "";
    public string? Transport { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? ProviderResponse { get; set; }
    public string? ErrorCode { get; set; }
    public string? IssueCode { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ContentPurgedAt { get; set; }
}

internal sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> message)
    {
        message.ToTable("OutboundMessages", "mail");
        message.HasKey(x => x.Id);
        message.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        message.Property(x => x.UpdatedAt).HasValueGenerator<CreationTime>();
        message.Property(x => x.Category).HasMaxLength(OutboundMessage.CategoryMaxLength);
        message.Property(x => x.Status).HasMaxLength(16);
        message.Property(x => x.IdempotencyKey).HasMaxLength(OutboundMessage.IdempotencyKeyMaxLength);
        message.Property(x => x.RecipientAddress).HasMaxLength(OutboundMessage.AddressMaxLength);
        message.Property(x => x.RecipientName).HasMaxLength(OutboundMessage.NameMaxLength);
        message.Property(x => x.RecipientMasked).HasMaxLength(OutboundMessage.AddressMaxLength);
        message.Property(x => x.RecipientHash).HasMaxLength(32).IsFixedLength();
        message.Property(x => x.FromAddress).HasMaxLength(OutboundMessage.AddressMaxLength);
        message.Property(x => x.Subject).HasMaxLength(OutboundMessage.SubjectMaxLength);
        message.Property(x => x.HeaderMessageId).HasMaxLength(255);
        message.Property(x => x.Transport).HasMaxLength(16);
        message.Property(x => x.ProviderResponse).HasMaxLength(OutboundMessage.ResponseMaxLength);
        message.Property(x => x.ErrorCode).HasMaxLength(80);
        message.Property(x => x.IssueCode).HasMaxLength(40);
        message.Property(x => x.TraceId).HasMaxLength(32);
        message.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        message.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
