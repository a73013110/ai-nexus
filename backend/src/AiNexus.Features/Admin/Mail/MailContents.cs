using AiNexus.Data.App.Mail;
using Microsoft.AspNetCore.DataProtection;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 敏感信內文的加解密（Data Protection，與登入 cookie 同一個金鑰圈）。金鑰圈遺失或換機未搬移時解不開，該信以 <c>mail_content_unavailable</c> 失敗，
/// 這是刻意的：驗證碼信本來就有時效，不值得為它另外保存金鑰。
/// </summary>
public sealed class MailContents(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("AiNexus.Mail.Content.v1");

    public OutboundMessageContent Create(Guid messageId, RenderedMail mail) => new()
    {
        MessageId = messageId,
        Protected = mail.Sensitive,
        Html = mail.Sensitive ? protector.Protect(mail.Html) : mail.Html,
        Text = mail.Sensitive ? protector.Protect(mail.Text) : mail.Text,
    };

    /// <summary>解密失敗（金鑰不在、密文損毀）回傳 null。</summary>
    public (string? Html, string Text)? Read(OutboundMessageContent content)
    {
        if (!content.Protected) return (content.Html, content.Text);
        try
        {
            return (content.Html is null ? null : protector.Unprotect(content.Html), protector.Unprotect(content.Text));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
