namespace AiNexus.Data.App.Mail;

/// <summary>認領順序（數字大的先寄），對應 <c>mail.OutboundMessages.Priority</c>（tinyint）。安全信不會卡在大量群發後面。</summary>
public enum MailPriority : byte
{
    Bulk = 0,
    Normal = 1,
    Security = 2,
}
