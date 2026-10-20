using AiNexus.Data.App.Mail;
using MimeKit;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>由 outbox 列與內文組成 MIME（multipart/alternative：純文字＋HTML）。</summary>
public static class MimeComposer
{
    public static MimeMessage Compose(OutboundMessage message, string address, string? html, string text, MailOptions options, DateTimeOffset now)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.From.DisplayName, message.FromAddress));
        mime.To.Add(new MailboxAddress(message.RecipientName ?? "", address));
        if (options.ReplyTo.Length > 0) mime.ReplyTo.Add(MailboxAddress.Parse(options.ReplyTo));
        mime.Subject = message.Subject;
        mime.Date = now;
        // 保存的是含角括號的標頭值；MimeKit 的 MessageId 不含角括號。
        mime.MessageId = message.HeaderMessageId.Trim('<', '>');
        // 系統信：避免 Exchange 的外出自動回覆與郵件迴圈。
        mime.Headers.Add("Auto-Submitted", "auto-generated");
        mime.Headers.Add("X-Auto-Response-Suppress", "All");
        var body = new BodyBuilder { TextBody = text, HtmlBody = html };
        mime.Body = body.ToMessageBody();
        return mime;
    }
}
