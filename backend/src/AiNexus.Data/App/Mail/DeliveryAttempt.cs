using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Mail;

/// <summary>一次寄送嘗試的結果（<see cref="MailAttemptOutcomes"/>）；純附屬資料，隨郵件刪除。</summary>
public sealed class DeliveryAttempt
{
    public Guid MessageId { get; set; }
    public int Attempt { get; set; }
    public string Transport { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public string Outcome { get; set; } = "";
    public int? SmtpStatus { get; set; }
    public string? EnhancedStatus { get; set; }
    public string? Response { get; set; }
    public string? ErrorCode { get; set; }
    public string? IssueCode { get; set; }
}

internal sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> attempt)
    {
        attempt.ToTable("DeliveryAttempts", "mail");
        attempt.HasKey(x => new { x.MessageId, x.Attempt });
        attempt.Property(x => x.Transport).HasMaxLength(16);
        attempt.Property(x => x.Outcome).HasMaxLength(16);
        attempt.Property(x => x.EnhancedStatus).HasMaxLength(16);
        attempt.Property(x => x.Response).HasMaxLength(OutboundMessage.ResponseMaxLength);
        attempt.Property(x => x.ErrorCode).HasMaxLength(80);
        attempt.Property(x => x.IssueCode).HasMaxLength(40);
        attempt.HasOne<OutboundMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
