using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 信件內文的語意區塊，同時寫出 HTML（table 版型、inline CSS）與純文字。所有值都經 <see cref="HtmlEncoder"/> 編碼；
/// 連結只接受站內路徑（<c>/</c> 開頭），origin 由 <c>Mail:PublicBaseUrl</c> 補上，不從請求 Host 取。
/// </summary>
public sealed class MailBody
{
    // 中文不轉成數字參照，信件原始碼才讀得懂；<、>、&、引號與控制字元仍會編碼。
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);
    private readonly StringBuilder html = new();
    private readonly StringBuilder text = new();
    private readonly string baseUrl;

    internal MailBody(string baseUrl) => this.baseUrl = baseUrl;

    internal string Html => html.ToString();
    internal string Text => text.ToString().TrimEnd() + "\n";

    public MailBody Heading(string value)
    {
        html.Append("<h1 style=\"margin:0 0 16px;font-size:20px;line-height:28px;font-weight:600;color:#1f2328;\">").Append(Encode(value)).Append("</h1>");
        text.Append(Plain(value)).Append("\n\n");
        return this;
    }

    public MailBody Paragraph(string value)
    {
        html.Append("<p style=\"margin:0 0 16px;font-size:15px;line-height:24px;color:#1f2328;\">").Append(Encode(value)).Append("</p>");
        text.Append(Plain(value)).Append("\n\n");
        return this;
    }

    /// <summary>驗證碼等需要照抄的短字串，以等寬大字呈現。</summary>
    public MailBody Code(string value)
    {
        html.Append("<p style=\"margin:0 0 16px;font-family:Consolas,'Courier New',monospace;font-size:28px;line-height:36px;letter-spacing:6px;font-weight:600;color:#1f2328;\">")
            .Append(Encode(value)).Append("</p>");
        text.Append("    ").Append(Plain(value)).Append("\n\n");
        return this;
    }

    /// <summary>站內連結按鈕；<paramref name="path"/> 必須是 <c>/</c> 開頭的站內路徑（例：<c>FeatureCatalog.RouteOf(...)</c>）。</summary>
    public MailBody Button(string label, string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') || path.Any(char.IsControl))
            throw new ArgumentException("Mail links must be site-relative paths.", nameof(path));
        var url = baseUrl + path;
        html.Append("<p style=\"margin:0 0 16px;\"><a href=\"").Append(Encode(url))
            .Append("\" style=\"display:inline-block;padding:10px 18px;border-radius:6px;background:#2563eb;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;\">")
            .Append(Encode(label)).Append("</a></p>");
        text.Append(Plain(label)).Append('：').Append(url).Append("\n\n");
        return this;
    }

    /// <summary>次要說明（例如「不是你本人操作請忽略」）。</summary>
    public MailBody Note(string value)
    {
        html.Append("<p style=\"margin:0 0 16px;font-size:13px;line-height:20px;color:#59636e;\">").Append(Encode(value)).Append("</p>");
        text.Append(Plain(value)).Append("\n\n");
        return this;
    }

    public MailBody KeyValue(string key, string value)
    {
        html.Append("<p style=\"margin:0 0 8px;font-size:14px;line-height:22px;color:#1f2328;\"><span style=\"color:#59636e;\">").Append(Encode(key))
            .Append("：</span>").Append(Encode(value)).Append("</p>");
        text.Append(Plain(key)).Append('：').Append(Plain(value)).Append('\n');
        return this;
    }

    // 換行保留為 <br>：其餘控制字元換成空白，避免純文字版被插入假標頭或終端機跳脫序列。
    private static string Encode(string value) => Encoder.Encode(Plain(value)).Replace("&#xA;", "<br>", StringComparison.Ordinal);

    private static string Plain(string value) => new(value.Replace("\r\n", "\n", StringComparison.Ordinal).Select(c => c == '\n' || !char.IsControl(c) ? c : ' ').ToArray());
}
