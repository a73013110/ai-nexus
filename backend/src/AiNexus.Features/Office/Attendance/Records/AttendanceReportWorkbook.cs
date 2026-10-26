using System.Globalization;
using AiNexus.Features.Office.Attendance.Forms;
using AiNexus.Features.Platform.Gd;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExportText = AiNexus.Platform.Export.Spreadsheet;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄匯出檔：逐格重現舊系統 EmpClock 的「出缺勤計算」，讓 HR 核對、列印與轉交的習慣不變。
/// 不走共用的 <see cref="ExportText.Write"/>（它固定加標題樣式、凍結、篩選與微軟正黑體），版面、字型、欄寬與列高都照舊檔寫死。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>單一工作表「出缺勤計算」：第一列是欄名（前兩欄空白），每人一段、每天一列，段與段之間（最後一人之後也是）空兩列。</item>
/// <item>每段第一列寫姓名與卡號，下面三列的前兩欄是「遲到(計)」「早退(計)」「異常(計)」與天數；不到四天的人補空列放小計。</item>
/// <item>遲到的上班、早退的下班以紅字標示；遲到、早退、異常欄在未補足時打「＊」（舊檔的記號），已補足寫「補足」、
/// 要等流程中的單才補足時寫「流程中」，表單無法取得時寫「未知」。小計照舊數所有遲到、早退、異常的天數。備註是當天的假單與出差單，多筆以「、」相連。</item>
/// <item>全部是 標楷體 13.875 點、置中（備註靠左）、列高 19.5；時間與日期照舊檔寫成文字。</item>
/// </list>
/// 列出哪些日子、怎麼判遲到早退異常，與畫面同一份查詢（<see cref="ListAttendanceRecords"/>），不另照舊系統的規則。
/// </remarks>
internal static class AttendanceReportWorkbook
{
    public const string SheetName = "出缺勤計算";
    public const string Mark = "＊";

    private const string FontName = "標楷體";
    private const double FontSize = 13.875, RowHeight = 19.5;
    // 舊檔的欄寬原值（含 Excel 的邊距），換算前就是這些數字。
    private static readonly double[] Widths = [13.9296875, 7.9296875, 24.9296875, 13.9296875, 13.9296875, 7.9296875, 7.9296875, 7.9296875, 35.9296875];
    private static readonly string[] Headers = ["", "", "日期", "上班打卡", "下班打卡", "遲到", "早退", "異常", "備註"];
    private static readonly string[] Counters = ["遲到(計)", "早退(計)", "異常(計)"];
    // cellXfs 的索引：0 預設、1 置中、2 靠左（備註）、3 置中紅字。
    private const uint Center = 1, Left = 2, Red = 3;

    /// <summary><paramref name="cards"/> 是使用者在區間內持有的卡號；沒有帳號的資料寫它依附的卡號。</summary>
    public static byte[] Write(AttendanceRecordsDto data, IReadOnlyDictionary<Guid, string> cards)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = Styles();
            var part = workbook.AddNewPart<WorksheetPart>();
            WriteSheet(part, data, cards);
            workbook.Workbook = new Workbook(new Sheets(new Sheet { Name = SheetName, SheetId = 1, Id = workbook.GetIdOfPart(part) }));
        }
        return stream.ToArray();
    }

    private static void WriteSheet(WorksheetPart part, AttendanceRecordsDto data, IReadOnlyDictionary<Guid, string> cards)
    {
        using var writer = OpenXmlWriter.Create(part);
        writer.WriteStartElement(new Worksheet());
        writer.WriteElement(new SheetViews(new SheetView { TabSelected = true, WorkbookViewId = 0 }));
        writer.WriteElement(new SheetFormatProperties { DefaultRowHeight = 15, CustomHeight = true });
        writer.WriteStartElement(new Columns());
        for (var i = 0; i < Widths.Length; i++)
            writer.WriteElement(new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = Widths[i], CustomWidth = true });
        writer.WriteEndElement();

        writer.WriteStartElement(new SheetData());
        uint number = 1;
        WriteRow(writer, number++, [.. Headers.Select(x => Str(x, Center))]);
        foreach (var person in data.People)
        {
            var card = person.UserId is { } id ? cards.GetValueOrDefault(id) : person.CardNumber;
            double[] counts =
            [
                person.Days.Count(x => x.LateSeconds > 0),
                person.Days.Count(x => x.EarlyLeaveSeconds > 0),
                person.Days.Count(x => x.Status == AttendanceStatus.Anomaly)
            ];
            var lines = Math.Max(person.Days.Count, 1 + Counters.Length);
            for (var i = 0; i < lines; i++)
            {
                List<Cell> cells = i switch
                {
                    0 => [Str(person.DisplayName, Center), Str(card ?? "", Center)],
                    <= 3 => [Str(Counters[i - 1], Center), Number(counts[i - 1])],
                    _ => [Str("", Center), Str("", Center)]
                };
                cells.AddRange(i < person.Days.Count ? Day(person.Days[i]) : [Blank(), Blank(), Blank(), Blank(), Blank(), Blank(), Str("", Left)]);
                WriteRow(writer, number++, cells);
            }
            WriteRow(writer, number++, []);
            WriteRow(writer, number++, []);
        }
        writer.WriteEndElement();

        writer.WriteElement(new PageMargins { Left = 0.7, Right = 0.7, Top = 0.75, Bottom = 0.75, Header = 0.3, Footer = 0.3 });
        writer.WriteEndElement();
    }

    /// <summary>日期到備註七格。只有一筆刷卡時上下班是同一筆，與畫面相同只寫在上班。</summary>
    private static Cell[] Day(AttendanceRecordDto day)
    {
        var late = day.LateSeconds > 0;
        var early = day.EarlyLeaveSeconds > 0;
        var singlePunch = day.ClockIn is not null && day.ClockIn == day.ClockOut;
        return
        [
            Str(Invariant($"{day.Date:yyyy/MM/dd} 星期{"日一二三四五六"[(int)day.Date.DayOfWeek]}"), Center),
            Clock(day.ClockIn, late),
            singlePunch ? Blank() : Clock(day.ClockOut, early),
            late ? Str(Marker(day.Coverage), Center) : Blank(),
            early ? Str(Marker(day.Coverage), Center) : Blank(),
            day.Status == AttendanceStatus.Anomaly ? Str(Marker(day.Coverage), Center) : Blank(),
            Str(Remark(day), Left)
        ];
    }

    /// <summary>舊檔的備註：【流程】【假別或出差單】單號〔起 ~ 迄〕，起訖是表單原值（台灣時間）。</summary>
    internal static string Remark(AttendanceRecordDto day)
    {
        if (day.FormState == FormState.Unavailable) return "無法取得";
        return string.Join("、", day.Forms.Select(x => Invariant(
            $"【{Flow(x.FlowStatus)}】【{(x.Kind == GdFormKind.Travel ? "出差單" : x.Title)}】{x.DesNo}〔{TaipeiTime.Of(x.StartsAt):yyyy-MM-dd HH:mm:ss} ~ {TaipeiTime.Of(x.EndsAt):yyyy-MM-dd HH:mm:ss}〕")));
    }

    /// <summary>
    /// 遲到、早退、異常欄的記號（當天的補足狀態）：未補足照舊打「＊」，其他寫兩三個字，列印出來不必對照說明；欄寬（舊檔 7.93）放得下三個字。
    /// </summary>
    internal static string Marker(GapCoverage? coverage) => coverage switch
    {
        GapCoverage.Covered => "補足",
        GapCoverage.Pending => "流程中",
        GapCoverage.Unknown => "未知",
        _ => Mark
    };

    /// <summary>公文系統的流程狀態用 GD 畫面的字樣（流程中、核准）；只讀這兩種，見 <see cref="GdFormReader"/>。</summary>
    internal static string Flow(GdFormFlowOver status) => GdFlowOver.Label(status);

    private static Cell Clock(DateTimeOffset? at, bool flagged)
        => at is { } value ? Str(Invariant($"{TaipeiTime.Of(value):HH:mm:ss}"), flagged ? Red : Center) : Blank();

    private static void WriteRow(OpenXmlWriter writer, uint number, IReadOnlyList<Cell> cells)
    {
        writer.WriteStartElement(new Row { RowIndex = number, Height = RowHeight, CustomHeight = true });
        for (var i = 0; i < cells.Count; i++)
        {
            cells[i].CellReference = (char)('A' + i) + number.ToString(CultureInfo.InvariantCulture);
            writer.WriteElement(cells[i]);
        }
        writer.WriteEndElement();
    }

    private static Cell Str(string value, uint style) => new()
    {
        DataType = CellValues.InlineString, StyleIndex = style,
        InlineString = new InlineString(new Text(ExportText.Clean(value)) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static Cell Number(double value) => new() { CellValue = new CellValue(value.ToString("R", CultureInfo.InvariantCulture)), StyleIndex = Center };

    /// <summary>舊檔空白的格子仍帶置中樣式，之後在 Excel 裡填字也是標楷體置中。</summary>
    private static Cell Blank() => new() { StyleIndex = Center };

    /// <summary>日期格式裡的 <c>/</c> 與 <c>:</c> 是文化相依的分隔符號，一律以不變文化格式化。</summary>
    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    private static Stylesheet Styles()
    {
        static Font Kai(Color color) => new(new FontSize { Val = FontSize }, color, new FontName { Val = FontName }, new FontFamilyNumbering { Val = 2 });
        static CellFormat Format(uint font, HorizontalAlignmentValues horizontal) => new(new Alignment { Horizontal = horizontal })
        { NumberFormatId = 0, FontId = font, FillId = 0, BorderId = 0, FormatId = 0, ApplyFont = true, ApplyAlignment = true };

        // 字型 0 是 Excel 預設（舊檔同樣保留），1 標楷體、2 標楷體紅字。
        var fonts = new Fonts(
            new Font(new FontSize { Val = 11 }, new Color { Theme = 1 }, new FontName { Val = "Calibri" }, new FontFamilyNumbering { Val = 2 }, new FontScheme { Val = FontSchemeValues.Minor }),
            Kai(new Color { Theme = 1 }),
            Kai(new Color { Rgb = "FFFF0000" })) { Count = 3 };
        return new Stylesheet(
            fonts,
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
            new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 },
            new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
            new CellFormats(
                new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
                Format(1, HorizontalAlignmentValues.Center),
                Format(1, HorizontalAlignmentValues.Left),
                Format(2, HorizontalAlignmentValues.Center)) { Count = 4 },
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
    }
}
