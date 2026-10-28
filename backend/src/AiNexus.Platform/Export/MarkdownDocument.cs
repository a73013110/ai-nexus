using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;
using MarkdownTable = Markdig.Extensions.Tables.Table;
using MarkdownRow = Markdig.Extensions.Tables.TableRow;
using MarkdownCell = Markdig.Extensions.Tables.TableCell;

namespace AiNexus.Platform.Export;

/// <summary>
/// 把一份 Markdown 文件（標題＋內文）轉成 Word 或交給 <see cref="PdfRenderer"/> 的列印 HTML，供各模組的文件匯出共用。
/// 原始 HTML 一律停用、視為文字；支援標題、清單、表格與程式區塊。
/// </summary>
public static class MarkdownDocument
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();

    /// <summary>A4 列印頁；<paramref name="caption"/> 是標題上方的小字（例如「AI NEXUS · 成果文件」）。</summary>
    public static string Html(string title, string markdown, string caption) => PrintPage.Html(title, caption, Markdown.ToHtml(markdown, Pipeline));

    public static byte[] Docx(string title, string content)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body());
            var body = main.Document.Body!;

            // 預設字型與行距；中文字體放在 EastAsia，英數字與中文才會各自使用正確字型。
            var style = main.AddNewPart<StyleDefinitionsPart>();
            style.Styles = new W.Styles(
                new W.DocDefaults(
                    new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                        new W.RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI", EastAsia = "Microsoft JhengHei" },
                        new W.FontSize { Val = "22" })),
                    new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(
                        new W.SpacingBetweenLines { After = "160", Line = "360", LineRule = W.LineSpacingRuleValues.Auto }))));

            body.Append(new W.Paragraph(
                new W.ParagraphProperties(new W.KeepNext(), new W.SpacingBetweenLines { After = "320" }),
                new W.Run(
                    new W.RunProperties(new W.Bold(), new W.FontSize { Val = "40" }),
                    new W.Text(title) { Space = SpaceProcessingModeValues.Preserve })));
            AppendBlocks(body, Markdown.Parse(content, Pipeline));
            body.Append(new W.SectionProperties(
                new W.PageSize { Width = 11906U, Height = 16838U },
                new W.PageMargin { Top = 1134, Bottom = 1247, Left = 1020U, Right = 1020U, Header = 567U, Footer = 567U }));

            document.PackageProperties.Title = title;
            document.PackageProperties.Creator = "AI Nexus";
            main.Document.Save();
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
                // 超過 16 欄的表格在 Word 中寬度不足，改成每列一段純文字。
                if (rows.Any(x => x.Count > 16))
                {
                    foreach (var row in rows)
                        parent.Append(TextParagraph(string.Join(" | ", row.OfType<MarkdownCell>().Select(Plain))));
                    continue;
                }

                var borders = new W.TableBorders
                {
                    TopBorder = new() { Val = W.BorderValues.Single, Size = 4U },
                    LeftBorder = new() { Val = W.BorderValues.Single, Size = 4U },
                    BottomBorder = new() { Val = W.BorderValues.Single, Size = 4U },
                    RightBorder = new() { Val = W.BorderValues.Single, Size = 4U },
                    InsideHorizontalBorder = new() { Val = W.BorderValues.Single, Size = 4U },
                    InsideVerticalBorder = new() { Val = W.BorderValues.Single, Size = 4U },
                };
                var columns = Math.Max(1, rows.Max(x => x.Count));
                var output = new W.Table(
                    new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct }, borders),
                    new W.TableGrid(Enumerable.Range(0, columns)
                        .Select(_ => new W.GridColumn { Width = (9866 / columns).ToString(CultureInfo.InvariantCulture) })));
                foreach (var row in rows)
                {
                    var target = new W.TableRow();
                    foreach (var cell in row.OfType<MarkdownCell>())
                    {
                        // OOXML 規定儲存格至少要有一個段落，缺少時 Word 會判定檔案損毀。
                        var value = new W.TableCell();
                        AppendBlocks(value, cell);
                        if (!value.Elements<W.Paragraph>().Any()) value.Append(new W.Paragraph());
                        target.Append(value);
                    }
                    output.Append(target);
                }
                parent.Append(output);
                parent.Append(new W.Paragraph());
            }
            else if (block is CodeBlock code)
            {
                foreach (var line in code.Lines.Lines.Take(code.Lines.Count))
                    parent.Append(new W.Paragraph(new W.Run(
                        new W.RunProperties(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }, new W.FontSize { Val = "20" }),
                        new W.Text(line.Slice.ToString()) { Space = SpaceProcessingModeValues.Preserve })));
            }
            else if (block is LeafBlock leaf)
            {
                var paragraph = new W.Paragraph();
                if (block is HeadingBlock heading)
                    paragraph.ParagraphProperties = new(new W.KeepNext(), new W.SpacingBetweenLines { Before = "240", After = "120" });
                var properties = block is HeadingBlock h
                    ? new W.RunProperties(new W.Bold(), new W.FontSize { Val = (h.Level == 1 ? 32 : h.Level == 2 ? 28 : 24).ToString(CultureInfo.InvariantCulture) })
                    : new W.RunProperties();
                if (prefix is not null) paragraph.Append(new W.Run(new W.Text(prefix)));
                if (leaf.Inline is not null)
                    Inline(paragraph, leaf.Inline, properties);
                else
                    paragraph.Append(new W.Run(new W.Text(leaf.Lines.ToString()) { Space = SpaceProcessingModeValues.Preserve }));
                parent.Append(paragraph);
            }
            else if (block is ListBlock list)
            {
                var number = 0;
                foreach (var child in list.OfType<ListItemBlock>())
                    AppendBlocks(parent, child, list.IsOrdered ? (++number) + ". " : "• ");
            }
            else if (block is ContainerBlock container)
            {
                AppendBlocks(parent, container, prefix);
            }
        }
    }
    private static string Plain(MarkdownCell cell) => string.Join(" ", cell.OfType<LeafBlock>().Select(x => x.Inline?.ToString() ?? x.Lines.ToString()));
    private static W.Paragraph TextParagraph(string text) => new(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    private static void Inline(W.Paragraph paragraph, ContainerInline container, W.RunProperties properties)
    {
        for (var item = container.FirstChild; item is not null; item = item.NextSibling)
        {
            var format = (W.RunProperties)properties.CloneNode(true);
            if (item is EmphasisInline emphasis)
            {
                format.Append(emphasis.DelimiterCount >= 2 ? (OpenXmlElement)new W.Bold() : new W.Italic());
                Inline(paragraph, emphasis, format);
            }
            else if (item is ContainerInline nested)
            {
                Inline(paragraph, nested, format);
            }
            else if (item is LineBreakInline)
            {
                paragraph.Append(new W.Run(new W.Break()));
            }
            else
            {
                var text = item switch
                {
                    LiteralInline literal => literal.Content.ToString(),
                    CodeInline code => code.Content,
                    AutolinkInline link => link.Url,
                    HtmlInline html => html.Tag,
                    _ => item.ToString() ?? "",
                };
                if (item is CodeInline) format.Append(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
                paragraph.Append(new W.Run(format, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }
    }
}
