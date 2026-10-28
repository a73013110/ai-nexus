using System.Net;

namespace AiNexus.Platform.Export;

/// <summary>
/// 交給 <see cref="PdfRenderer"/> 的 A4 列印頁：版面、中文字型、標題不落在頁尾、表格標題列每頁重複等規則都在這裡，
/// 呼叫端只提供已安全編碼的內文 HTML。日後的表格報表也套同一頁，PDF 才會長得一致。
/// </summary>
/// <remarks>CSP 禁止載入任何外部資源；<see cref="PdfRenderer"/> 另外攔截全部網路請求，兩層各自成立。</remarks>
public static class PrintPage
{
    private const string Style =
        "@page{size:A4;margin:20mm 18mm 22mm}*{box-sizing:border-box}body{font:12pt/1.8 'Microsoft JhengHei','Noto Sans CJK TC',sans-serif;color:#242a2d;overflow-wrap:anywhere}h1{font-size:24pt;line-height:1.4;margin:0 0 20pt}h2{font-size:18pt}h3{font-size:15pt}h1,h2,h3,h4{break-after:avoid}p,li{orphans:3;widows:3}table{border-collapse:collapse;width:100%;font-size:10pt;table-layout:fixed}th,td{border:1px solid #dce0da;padding:7pt;text-align:left;overflow-wrap:anywhere}thead{display:table-header-group}tr{break-inside:avoid}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f2ee;padding:10pt;line-height:1.6;font-size:10pt}blockquote{border-left:3px solid #087e83;margin-left:0;padding-left:12pt;color:#59615f}img{display:none}a{color:#273f51;text-decoration:none}.meta{font-size:9pt;color:#65706a;border-bottom:1px solid #dce0da;padding-bottom:12pt}";

    public static string Html(string title, string caption, string body) =>
        "<!doctype html><html lang=\"zh-Hant\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\"><style>" + Style +
        "</style></head><body><p class=\"meta\">" + WebUtility.HtmlEncode(caption) + "</p><h1>" + WebUtility.HtmlEncode(title) + "</h1>" + body + "</body></html>";
}
