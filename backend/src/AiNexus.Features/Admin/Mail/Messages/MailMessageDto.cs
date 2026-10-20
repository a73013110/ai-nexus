using AiNexus.Data.App.Mail;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 寄送紀錄的一列。收件者只有遮罩形式；<paramref name="NextAttemptAt"/> 在未來且狀態 queued＝等待重試。
/// <paramref name="Priority"/> 是 <c>security</c>、<c>normal</c> 或 <c>bulk</c>。
/// </summary>
public sealed record MailMessageDto(Guid Id, DateTimeOffset CreatedAt, string Status, string Category, string Priority, string Recipient, string Subject,
    int Attempts, DateTimeOffset NextAttemptAt, DateTimeOffset? SentAt, string? ErrorCode, bool Sensitive)
{
    internal static MailMessageDto From(OutboundMessage x)
        => new(x.Id, x.CreatedAt, x.Status, x.Category, x.Priority.ToString().ToLowerInvariant(), x.RecipientMasked, x.Subject, x.Attempts, x.NextAttemptAt, x.SentAt, x.ErrorCode, x.Sensitive);
}

/// <summary>管理端的入列結果（重寄、測試信）。</summary>
public sealed record MailEnqueuedDto(Guid MessageId, bool Duplicate);
