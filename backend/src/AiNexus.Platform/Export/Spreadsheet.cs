using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AiNexus.Platform.Export;

/// <summary>
/// 把表格寫成 .xlsx（Office Open XML），供各列表的「匯出 Excel」共用；只做資料表格，不做公式、合併儲存格或圖表。
/// 每張工作表的標題列凍結、加篩選並在列印時每頁重複，橫向列印、寬度縮成一頁；字型用微軟正黑體，中文在 Excel 預設字型下才不會缺字或對不齊。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>文字一律寫成字串儲存格（inline string），開頭是 <c>=</c> 也不會被當成公式，不需要 CSV 那種單引號前綴防護。</item>
/// <item>XML 不允許的控制字元會被移除，超過 Excel 單格上限（32,767 字）的文字會截斷，避免整個檔案打不開。</item>
/// <item>工作表以 <see cref="OpenXmlWriter"/> 逐列寫出，不建整棵 DOM；一次匯出數萬列也只佔輸出檔大小的記憶體。</item>
/// </list>
/// </remarks>
public static class Spreadsheet
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Excel 單一儲存格的文字上限。</summary>
    public const int MaxCellText = 32767;

    private const string FontName = "Microsoft JhengHei";
    private const uint DateFormat = 164, TimeFormat = 165, DateTimeFormat = 166, DurationFormat = 167;

    /// <summary>值的格式，順序就是 <see cref="Styles"/> 裡 cellXfs 的分組順序。</summary>
    private enum Format { General, Date, Time, DateTime, Duration }

    private static readonly uint[] FormatIds = [0, DateFormat, TimeFormat, DateTimeFormat, DurationFormat];
    private static readonly int Tones = Enum.GetValues<SpreadsheetTone>().Length;
    /// <summary>標題列的樣式排在所有「格式 × 強調」組合之後。</summary>
    private static readonly uint HeaderStyle = (uint)(1 + FormatIds.Length * Tones);

    public static byte[] Write(IReadOnlyList<SpreadsheetSheet> sheets)
    {
        if (sheets.Count == 0) throw new ArgumentException("A workbook needs at least one worksheet.", nameof(sheets));
        if (sheets.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sheets.Count)
            throw new ArgumentException("Worksheet names must be unique.", nameof(sheets));

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = Styles();
            var list = new Sheets();
            var names = new DefinedNames();
            for (var index = 0; index < sheets.Count; index++)
            {
                var sheet = sheets[index];
                var part = workbook.AddNewPart<WorksheetPart>();
                WriteSheet(part, sheet);
                list.Append(new Sheet { Name = sheet.Name, SheetId = (uint)index + 1, Id = workbook.GetIdOfPart(part) });
                var quoted = "'" + sheet.Name.Replace("'", "''", StringComparison.Ordinal) + "'";
                if (sheet.Filter)
                    names.Append(new DefinedName($"{quoted}!$A$1:${ColumnName(sheet.Columns.Count - 1)}${sheet.Rows.Count + 1}") { Name = "_xlnm._FilterDatabase", LocalSheetId = (uint)index, Hidden = true });
                names.Append(new DefinedName($"{quoted}!$1:$1") { Name = "_xlnm.Print_Titles", LocalSheetId = (uint)index });
            }
            workbook.Workbook = new Workbook(list, names);
        }
        return stream.ToArray();
    }

    private static void WriteSheet(WorksheetPart part, SpreadsheetSheet sheet)
    {
        var last = ColumnName(sheet.Columns.Count - 1);
        using var writer = OpenXmlWriter.Create(part);
        writer.WriteStartElement(new Worksheet());
        writer.WriteElement(new SheetProperties(new PageSetupProperties { FitToPage = true }));
        // 凍結第一列：捲動時標題留在畫面上。
        writer.WriteElement(new SheetViews(new SheetView(
            new Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen },
            new Selection { Pane = PaneValues.BottomLeft, ActiveCell = "A2", SequenceOfReferences = new ListValue<StringValue> { InnerText = "A2" } })
        { WorkbookViewId = 0 }));
        writer.WriteStartElement(new Columns());
        for (var i = 0; i < sheet.Columns.Count; i++)
            writer.WriteElement(new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = sheet.Columns[i].Width, CustomWidth = true });
        writer.WriteEndElement();

        writer.WriteStartElement(new SheetData());
        WriteRow(writer, 1, sheet.Columns.Select(x => new SpreadsheetCell(x.Header)).ToArray(), header: true);
        for (var i = 0; i < sheet.Rows.Count; i++) WriteRow(writer, (uint)i + 2, sheet.Rows[i], header: false);
        writer.WriteEndElement();

        if (sheet.Filter) writer.WriteElement(new AutoFilter { Reference = $"A1:{last}{sheet.Rows.Count + 1}" });
        writer.WriteElement(new PageMargins { Left = 0.4, Right = 0.4, Top = 0.5, Bottom = 0.5, Header = 0.3, Footer = 0.3 });
        // A4 橫向、寬度縮成一頁、高度不限（FitToHeight = 0）。
        writer.WriteElement(new PageSetup { PaperSize = 9, Orientation = OrientationValues.Landscape, FitToWidth = 1, FitToHeight = 0 });
        if (sheet.PageBreaks.Count > 0)
        {
            var breaks = new RowBreaks { Count = (uint)sheet.PageBreaks.Count, ManualBreakCount = (uint)sheet.PageBreaks.Count };
            // Break.Id 是分頁線「上方」那一列的列號（1 起算）；資料列 i 在第 i + 2 列，所以它上方是 i + 1。
            foreach (var index in sheet.PageBreaks)
                breaks.Append(new Break { Id = (uint)index + 1, Max = 16383, ManualPageBreak = true });
            writer.WriteElement(breaks);
        }
        writer.WriteEndElement();
    }

    private static void WriteRow(OpenXmlWriter writer, uint number, IReadOnlyList<SpreadsheetCell> cells, bool header)
    {
        writer.WriteStartElement(new Row { RowIndex = number });
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = ToCell(cells[i], header);
            if (cell is null) continue;
            cell.CellReference = ColumnName(i) + number.ToString(CultureInfo.InvariantCulture);
            writer.WriteElement(cell);
        }
        writer.WriteEndElement();
    }

    private static Cell? ToCell(SpreadsheetCell cell, bool header)
    {
        uint Style(Format format) => header ? HeaderStyle : (uint)(1 + (int)format * Tones + (int)cell.Tone);
        Cell Number(double value, Format format) => new() { CellValue = new CellValue(value.ToString("R", CultureInfo.InvariantCulture)), StyleIndex = Style(format) };

        return cell.Value switch
        {
            null => null,
            string text => new Cell { DataType = CellValues.InlineString, StyleIndex = Style(Format.General), InlineString = new InlineString(new Text(Clean(text)) { Space = SpaceProcessingModeValues.Preserve }) },
            bool flag => new Cell { DataType = CellValues.Boolean, CellValue = new CellValue(flag ? "1" : "0"), StyleIndex = Style(Format.General) },
            int value => Number(value, Format.General),
            long value => Number(value, Format.General),
            double value => Number(value, Format.General),
            decimal value => new Cell { CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)), StyleIndex = Style(Format.General) },
            DateOnly date => Number(date.ToDateTime(TimeOnly.MinValue).ToOADate(), Format.Date),
            TimeOnly time => Number(time.ToTimeSpan().TotalDays, Format.Time),
            DateTime moment => Number(DateTime.SpecifyKind(moment, DateTimeKind.Unspecified).ToOADate(), Format.DateTime),
            TimeSpan span => Number(span.TotalDays, Format.Duration),
            _ => throw new ArgumentException($"{cell.Value.GetType().Name} cannot be written to a spreadsheet cell.", nameof(cell))
        };
    }

    /// <summary>
    /// 移除 XML 1.0 不允許的字元（Tab、換行以外的控制字元、單獨的代理字元），並截在 Excel 單格上限內。
    /// 版面固定、不走 <see cref="Write"/> 的匯出（例如照舊系統格式重現的報表）寫文字時也要先經過這裡。
    /// </summary>
    public static string Clean(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxCellText));
        for (var i = 0; i < text.Length && builder.Length < MaxCellText; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                if (builder.Length + 2 > MaxCellText) break;
                builder.Append(c).Append(text[++i]);
            }
            else if (c is '\t' or '\n' or '\r' || (c >= ' ' && !char.IsSurrogate(c) && c is not ('￾' or '￿'))) builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>0 起算的欄位索引換成欄名（0 → A、26 → AA）。</summary>
    internal static string ColumnName(int index)
    {
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    /// <summary>
    /// 樣式表：cellXfs 0 是預設；之後依 <see cref="Format"/> × <see cref="SpreadsheetTone"/> 排列（索引 1 + 格式 × 強調數 + 強調），最後一個是標題列。
    /// </summary>
    private static Stylesheet Styles()
    {
        static Font MakeFont(bool bold = false, string? color = null)
        {
            var font = new Font();
            if (bold) font.Append(new Bold());
            font.Append(new FontSize { Val = 11 });
            if (color is not null) font.Append(new Color { Rgb = color });
            font.Append(new FontName { Val = FontName }, new FontFamilyNumbering { Val = 2 }, new FontCharSet { Val = 136 });
            return font;
        }

        // 字型 0 一般、1 粗體、2 深紅，順序與 SpreadsheetTone 相同。
        var fonts = new Fonts(MakeFont(), MakeFont(bold: true), MakeFont(color: "FFC00000")) { Count = 3 };
        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFF2F2F2" }) { PatternType = PatternValues.Solid })) { Count = 3 };
        var borders = new Borders(
            new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()),
            new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin }, new DiagonalBorder())) { Count = 2 };
        var formats = new CellFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 });
        foreach (var format in FormatIds)
        {
            for (var tone = 0; tone < Tones; tone++)
            {
                formats.Append(new CellFormat(new Alignment { Vertical = VerticalAlignmentValues.Top })
                {
                    NumberFormatId = format, FontId = (uint)tone, FillId = 0, BorderId = 0, FormatId = 0,
                    ApplyNumberFormat = format != 0, ApplyFont = tone != 0, ApplyAlignment = true
                });
            }
        }
        formats.Append(new CellFormat(new Alignment { Vertical = VerticalAlignmentValues.Center })
        { NumberFormatId = 0, FontId = 1, FillId = 2, BorderId = 1, FormatId = 0, ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true });
        formats.Count = (uint)formats.ChildElements.Count;

        return new Stylesheet(
            new NumberingFormats(
                new NumberingFormat { NumberFormatId = DateFormat, FormatCode = "yyyy/mm/dd" },
                new NumberingFormat { NumberFormatId = TimeFormat, FormatCode = "hh:mm:ss" },
                new NumberingFormat { NumberFormatId = DateTimeFormat, FormatCode = "yyyy/mm/dd hh:mm:ss" },
                new NumberingFormat { NumberFormatId = DurationFormat, FormatCode = "[h]:mm:ss" }) { Count = 4 },
            fonts,
            fills,
            borders,
            new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
            formats,
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
    }
}
