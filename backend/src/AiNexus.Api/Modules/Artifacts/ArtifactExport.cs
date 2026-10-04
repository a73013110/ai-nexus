using System.Net;
using System.Text;
using AiNexus.BuildingBlocks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using W = DocumentFormat.OpenXml.Wordprocessing;
using MarkdownTable = Markdig.Extensions.Tables.Table;
using MarkdownRow = Markdig.Extensions.Tables.TableRow;
using MarkdownCell = Markdig.Extensions.Tables.TableCell;

namespace AiNexus.Modules.Artifacts;

public sealed class ExportOptions { public string BrowserChannel { get; set; } = "msedge"; public int TimeoutSeconds { get; set; } = 30; }
public sealed record ExportFile(byte[] Data, string ContentType);
public sealed class ArtifactExport(PdfExportRenderer pdf)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();
    public async Task<ExportFile> ExportAsync(ArtifactDto document, string format, CancellationToken ct) => format switch
    {
        "md" => new(Encoding.UTF8.GetBytes(document.Content), "text/markdown; charset=utf-8"),
        "docx" => new(Word(document.Resource.Name, document.Content), "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        "pdf" => new(await pdf.RenderAsync(Html(document.Resource.Name, document.Content), ct), "application/pdf"),
        _ => throw new ApiException(400, "export_format_invalid", "支援 Word、PDF 或 Markdown 匯出。"),
    };
    public static string Html(string title, string content)
    {
        // Raw HTML is disabled; all network requests are separately blocked by the renderer.
        var markdown = Markdown.ToHtml(content, Pipeline);
        return "<!doctype html><html lang=\"zh-Hant\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\"><style>" +
            "@page{size:A4;margin:20mm 18mm 22mm}*{box-sizing:border-box}body{font:12pt/1.8 'Microsoft JhengHei','Noto Sans CJK TC',sans-serif;color:#242a2d;overflow-wrap:anywhere}h1{font-size:24pt;line-height:1.4;margin:0 0 20pt}h2{font-size:18pt}h3{font-size:15pt}h1,h2,h3,h4{break-after:avoid}p,li{orphans:3;widows:3}table{border-collapse:collapse;width:100%;font-size:10pt;table-layout:fixed}th,td{border:1px solid #dce0da;padding:7pt;text-align:left;overflow-wrap:anywhere}thead{display:table-header-group}tr{break-inside:avoid}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f2ee;padding:10pt;line-height:1.6;font-size:10pt}blockquote{border-left:3px solid #087e83;margin-left:0;padding-left:12pt;color:#59615f}img{display:none}a{color:#273f51;text-decoration:none}.meta{font-size:9pt;color:#65706a;border-bottom:1px solid #dce0da;padding-bottom:12pt}" +
            "</style></head><body><p class=\"meta\">AI NEXUS · 成果文件</p><h1>" + WebUtility.HtmlEncode(title) + "</h1>" + markdown + "</body></html>";
    }
    public static byte[] Word(string title, string content)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart(); main.Document = new W.Document(new W.Body()); var body = main.Document.Body!;
            var style = main.AddNewPart<StyleDefinitionsPart>(); style.Styles = new W.Styles(new W.DocDefaults(new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(new W.RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI", EastAsia = "Microsoft JhengHei" }, new W.FontSize { Val = "22" })), new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(new W.SpacingBetweenLines { After = "160", Line = "360", LineRule = W.LineSpacingRuleValues.Auto }))));
            body.Append(new W.Paragraph(new W.ParagraphProperties(new W.KeepNext(), new W.SpacingBetweenLines { After = "320" }), new W.Run(new W.RunProperties(new W.Bold(), new W.FontSize { Val = "40" }), new W.Text(title) { Space = SpaceProcessingModeValues.Preserve })));
            AppendBlocks(body, Markdown.Parse(content, Pipeline));
            body.Append(new W.SectionProperties(new W.PageSize { Width = 11906U, Height = 16838U }, new W.PageMargin { Top = 1134, Bottom = 1247, Left = 1020U, Right = 1020U, Header = 567U, Footer = 567U }));
            document.PackageProperties.Title = title; document.PackageProperties.Creator = "AI Nexus"; main.Document.Save();
        }
        return stream.ToArray();
    }
    private static void AppendBlocks(OpenXmlCompositeElement parent, IEnumerable<Block> blocks, string? prefix = null)
    {
        foreach (var block in blocks)
        {
            if (block is MarkdownTable table)
            {
                var rows = table.OfType<MarkdownRow>().ToArray();
                if (rows.Any(x => x.Count > 16)) { foreach (var row in rows) parent.Append(TextParagraph(string.Join(" | ", row.OfType<MarkdownCell>().Select(Plain)))); continue; }
                var borders = new W.TableBorders { TopBorder = new() { Val = W.BorderValues.Single, Size = 4U }, LeftBorder = new() { Val = W.BorderValues.Single, Size = 4U }, BottomBorder = new() { Val = W.BorderValues.Single, Size = 4U }, RightBorder = new() { Val = W.BorderValues.Single, Size = 4U }, InsideHorizontalBorder = new() { Val = W.BorderValues.Single, Size = 4U }, InsideVerticalBorder = new() { Val = W.BorderValues.Single, Size = 4U } };
                var columns = Math.Max(1, rows.Max(x => x.Count));
                var output = new W.Table(new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct }, borders), new W.TableGrid(Enumerable.Range(0, columns).Select(_ => new W.GridColumn { Width = (9866 / columns).ToString() })));
                foreach (var row in rows) { var target = new W.TableRow(); foreach (var cell in row.OfType<MarkdownCell>()) { var value = new W.TableCell(); AppendBlocks(value, cell); if (!value.Elements<W.Paragraph>().Any()) value.Append(new W.Paragraph()); target.Append(value); } output.Append(target); }
                parent.Append(output); parent.Append(new W.Paragraph());
            }
            else if (block is CodeBlock code) { foreach (var line in code.Lines.Lines.Take(code.Lines.Count)) parent.Append(new W.Paragraph(new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }, new W.FontSize { Val = "20" }), new W.Text(line.Slice.ToString()) { Space = SpaceProcessingModeValues.Preserve }))); }
            else if (block is LeafBlock leaf)
            {
                var paragraph = new W.Paragraph(); if (block is HeadingBlock heading) paragraph.ParagraphProperties = new(new W.KeepNext(), new W.SpacingBetweenLines { Before = "240", After = "120" });
                var properties = block is HeadingBlock h ? new W.RunProperties(new W.Bold(), new W.FontSize { Val = (h.Level == 1 ? 32 : h.Level == 2 ? 28 : 24).ToString() }) : new W.RunProperties();
                if (prefix is not null) paragraph.Append(new W.Run(new W.Text(prefix)));
                if (leaf.Inline is not null) Inline(paragraph, leaf.Inline, properties); else paragraph.Append(new W.Run(new W.Text(leaf.Lines.ToString()) { Space = SpaceProcessingModeValues.Preserve }));
                parent.Append(paragraph);
            }
            else if (block is ListBlock list) { var number = 0; foreach (var child in list.OfType<ListItemBlock>()) AppendBlocks(parent, child, list.IsOrdered ? (++number) + ". " : "• "); }
            else if (block is ContainerBlock container) AppendBlocks(parent, container, prefix);
        }
    }
    private static string Plain(MarkdownCell cell) => string.Join(" ", cell.OfType<LeafBlock>().Select(x => x.Inline?.ToString() ?? x.Lines.ToString()));
    private static W.Paragraph TextParagraph(string text) => new(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    private static void Inline(W.Paragraph paragraph, ContainerInline container, W.RunProperties properties)
    {
        for (var item = container.FirstChild; item is not null; item = item.NextSibling)
        {
            var format = (W.RunProperties)properties.CloneNode(true);
            if (item is EmphasisInline emphasis) { format.Append(emphasis.DelimiterCount >= 2 ? (OpenXmlElement)new W.Bold() : new W.Italic()); Inline(paragraph, emphasis, format); }
            else if (item is ContainerInline nested) Inline(paragraph, nested, format);
            else if (item is LineBreakInline) paragraph.Append(new W.Run(new W.Break()));
            else { var text = item switch { LiteralInline literal => literal.Content.ToString(), CodeInline code => code.Content, AutolinkInline link => link.Url, HtmlInline html => html.Tag, _ => item.ToString() ?? "" }; if (item is CodeInline) format.Append(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }); paragraph.Append(new W.Run(format, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })); }
        }
    }
}

public sealed class PdfExportRenderer(IOptions<ExportOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim concurrency = new(2, 2);
    private readonly SemaphoreSlim startup = new(1, 1);
    private IPlaywright? driver;
    private IBrowser? browser;
    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        if (!await concurrency.WaitAsync(0, ct)) throw new ApiException(429, "pdf_export_busy", "PDF 匯出正在忙碌，請稍後重試。");
        IBrowserContext? context = null;
        try
        {
            await startup.WaitAsync(ct);
            try { if (browser?.IsConnected != true) { driver ??= await Playwright.CreateAsync(); browser = await driver.Chromium.LaunchAsync(new() { Channel = options.Value.BrowserChannel == "chromium" ? null : options.Value.BrowserChannel, Headless = true, Timeout = options.Value.TimeoutSeconds * 1000 }); } ct.ThrowIfCancellationRequested(); }
            finally { startup.Release(); }
            context = await browser.NewContextAsync(new() { JavaScriptEnabled = false, ServiceWorkers = ServiceWorkerPolicy.Block, AcceptDownloads = false }); ct.ThrowIfCancellationRequested();
            await context.RouteAsync("**/*", route => route.AbortAsync());
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(options.Value.TimeoutSeconds * 1000);
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            return await page.PdfAsync(new() { Format = "A4", PrintBackground = true, PreferCSSPageSize = true, DisplayHeaderFooter = true, HeaderTemplate = "<span></span>", FooterTemplate = "<div style=\"font:9px sans-serif;width:100%;text-align:center;color:#65706a\"><span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></div>" }).WaitAsync(TimeSpan.FromSeconds(options.Value.TimeoutSeconds), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is PlaywrightException or TimeoutException) { throw new ApiException(503, "pdf_renderer_unavailable", "PDF 匯出暫時無法使用，請由管理員檢查匯出瀏覽器安裝與執行權限；可先匯出 Word。 "); }
        finally { try { if (context is not null) await context.CloseAsync(); } catch (PlaywrightException) { } finally { concurrency.Release(); } }
    }
    public async ValueTask DisposeAsync() { if (browser is not null) await browser.CloseAsync(); driver?.Dispose(); concurrency.Dispose(); startup.Dispose(); }
}
