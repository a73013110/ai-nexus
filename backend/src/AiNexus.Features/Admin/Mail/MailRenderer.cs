using System.Text.RegularExpressions;
using AiNexus.Data.App.Mail;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail;

/// <summary>渲染結果；入列時保存，寄出的就是這份內容。</summary>
public sealed record RenderedMail(string Category, MailPriority Priority, bool Sensitive, string Subject, string Html, string Text);

/// <summary>把範本套上共用版型（產品名、頁尾），並檢查分類與大小上限。超過上限是範本的程式錯誤，直接拋出。</summary>
public sealed partial class MailRenderer(IOptions<MailOptions> options)
{
    public const string ProductName = "AI Nexus";
    public const string Footer = "此信由系統自動寄出，請勿直接回覆。";
    public const int HtmlMaxLength = 256 * 1024, TextMaxLength = 128 * 1024;

    public RenderedMail Render<TModel>(MailTemplate<TModel> template, TModel model)
    {
        if (template.Category.Length > OutboundMessage.CategoryMaxLength || !CategoryPattern().IsMatch(template.Category))
            throw new ArgumentException($"Mail category '{template.Category}' must be lowercase dotted or kebab-case within {OutboundMessage.CategoryMaxLength} characters.", nameof(template));
        var subject = Subject(template.Subject(model));
        var body = new MailBody(options.Value.PublicBaseUrl);
        template.Body(model, body);
        var html = Layout(subject, body.Html);
        var text = $"{ProductName}\n\n{body.Text}\n--\n{Footer}\n";
        if (html.Length > HtmlMaxLength || text.Length > TextMaxLength)
            throw new InvalidOperationException($"Mail '{template.Category}' exceeds the body size limit.");
        return new(template.Category, template.Priority, template.Sensitive, subject, html, text);
    }

    /// <summary>主旨去掉換行與控制字元（標頭注入），截到 200 字元；空主旨用產品名。</summary>
    public static string Subject(string value)
    {
        var plain = string.Join(' ', new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (plain.Length == 0) plain = ProductName;
        return plain.Length > OutboundMessage.SubjectMaxLength ? plain[..OutboundMessage.SubjectMaxLength] : plain;
    }

    private static string Layout(string subject, string content)
    {
        const string font = "font-family:'Segoe UI','Microsoft JhengHei',Arial,sans-serif;";
        var title = System.Net.WebUtility.HtmlEncode(subject);
        return "<!DOCTYPE html><html lang=\"zh-Hant\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\">"
            + $"<title>{title}</title></head><body style=\"margin:0;padding:0;background:#f6f8fa;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f6f8fa;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#ffffff;border:1px solid #d1d9e0;border-radius:8px;\">"
            + $"<tr><td style=\"padding:20px 28px 0;{font}font-size:13px;font-weight:600;color:#59636e;\">{ProductName}</td></tr>"
            + $"<tr><td style=\"padding:16px 28px 8px;{font}\">{content}</td></tr>"
            + $"<tr><td style=\"padding:16px 28px 24px;border-top:1px solid #d1d9e0;{font}font-size:12px;line-height:18px;color:#59636e;\">{Footer}</td></tr>"
            + "</table></td></tr></table></body></html>";
    }

    [GeneratedRegex("^[a-z0-9]+([.-][a-z0-9]+)*$")]
    private static partial Regex CategoryPattern();
}
