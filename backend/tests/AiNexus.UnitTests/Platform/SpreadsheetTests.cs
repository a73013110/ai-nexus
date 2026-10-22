using AiNexus.Platform.Export;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AiNexus.UnitTests.Platform;

public sealed class SpreadsheetTests
{
    [Fact]
    public void WritesTypedCellsWithFrozenFilteredHeaderAndReadableChineseText()
    {
        var sheet = new SpreadsheetSheet("出勤", [new("姓名", 12), new("日期", 12), new("上班", 10), new("遲到", 10), new("天數", 8), new("備註", 30)]);
        sheet.Add("王小明", new DateOnly(2026, 10, 19), new SpreadsheetCell(new TimeOnly(9, 5, 30), SpreadsheetTone.Danger), TimeSpan.FromSeconds(330), 3, "=HYPERLINK(\"x\")");
        sheet.Add("陳小華", null, null, null, 0);

        using var document = Open(Spreadsheet.Write([sheet]));
        var workbook = document.WorkbookPart!;
        var worksheet = workbook.WorksheetParts.Single().Worksheet!;

        Assert.Equal("出勤", workbook.Workbook!.Sheets!.Elements<Sheet>().Single().Name!.Value);
        var pane = worksheet.Descendants<Pane>().Single();
        Assert.Equal((PaneStateValues.Frozen, 1d, "A2"), (pane.State!.Value, pane.VerticalSplit!.Value, pane.TopLeftCell!.Value));
        Assert.Equal("A1:F3", worksheet.Descendants<AutoFilter>().Single().Reference!.Value);
        Assert.Equal([12d, 12, 10, 10, 8, 30], worksheet.Descendants<Column>().Select(x => x.Width!.Value));
        Assert.Contains(workbook.Workbook!.DefinedNames!.Elements<DefinedName>(), x => x.Name == "_xlnm.Print_Titles" && x.Text == "'出勤'!$1:$1");

        var rows = worksheet.Descendants<Row>().ToList();
        Assert.Equal(["姓名", "日期", "上班", "遲到", "天數", "備註"], rows[0].Elements<Cell>().Select(Text));
        var cells = rows[1].Elements<Cell>().ToList();
        Assert.Equal("王小明", Text(cells[0]));
        Assert.Equal(new DateOnly(2026, 10, 19).ToDateTime(TimeOnly.MinValue).ToOADate(), Number(cells[1]));
        Assert.Equal("yyyy/mm/dd", FormatCode(workbook, cells[1]));
        Assert.Equal(new TimeSpan(9, 5, 30).TotalDays, Number(cells[2]), 9);
        Assert.Equal("hh:mm:ss", FormatCode(workbook, cells[2]));
        Assert.Equal("FFC00000", Font(workbook, cells[2]).Color!.Rgb!.Value);
        Assert.Equal(TimeSpan.FromSeconds(330).TotalDays, Number(cells[3]), 9);
        Assert.Equal("[h]:mm:ss", FormatCode(workbook, cells[3]));
        Assert.Equal(3, Number(cells[4]));
        // 開頭是 = 的文字仍是字串儲存格，不會被當成公式。
        Assert.Equal((CellValues.InlineString, "=HYPERLINK(\"x\")"), (cells[5].DataType!.Value, Text(cells[5])));
        Assert.Null(cells[5].CellFormula);
        Assert.Equal("Microsoft JhengHei", Font(workbook, cells[0]).GetFirstChild<FontName>()!.Val!.Value);
        // 空白的儲存格不寫出，後面的值仍在正確的欄。
        Assert.Equal(["A3", "E3"], rows[2].Elements<Cell>().Select(x => x.CellReference!.Value));
    }

    [Fact]
    public void PageBreaksSitAboveTheRowsTheyStartAndSectionedSheetsCanSkipTheFilter()
    {
        var sheet = new SpreadsheetSheet("說明", [new("項目", 10)]) { Filter = false };
        sheet.BreakPage().Add("第一段").Add("第一段").BreakPage().Add("第二段").BreakPage().BreakPage().Add("第三段");

        using var document = Open(Spreadsheet.Write([sheet, new SpreadsheetSheet("統計", [new("人數", 8)])]));
        var worksheet = document.WorkbookPart!.WorksheetParts.First(x => x.Worksheet!.Descendants<Row>().Count() > 1).Worksheet!;

        // 資料列 2、3 在第 4、5 列；分頁線在它們上方（第 3、4 列之後）。還沒有資料時與連續的分頁不重複。
        Assert.Equal([3u, 4u], worksheet.Descendants<Break>().Select(x => x.Id!.Value));
        Assert.Empty(worksheet.Descendants<AutoFilter>());
        Assert.Equal(["說明", "統計"], document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(x => x.Name!.Value));
    }

    [Fact]
    public void ControlCharactersAreRemovedAndLongTextIsTruncated()
    {
        var sheet = new SpreadsheetSheet("文字", [new("內容", 10)]);
        sheet.Add("第一行\u0001\n第二行￿\uD800");
        sheet.Add(new string('長', Spreadsheet.MaxCellText + 10));

        using var document = Open(Spreadsheet.Write([sheet]));
        var cells = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Row>().Skip(1).Select(x => Text(x.Elements<Cell>().Single())).ToList();

        Assert.Equal("第一行\n第二行", cells[0]);
        Assert.Equal(Spreadsheet.MaxCellText, cells[1].Length);
    }

    [Fact]
    public void InvalidSheetsAndValuesAreRejectedWhenTheyAreBuilt()
    {
        Assert.Throws<ArgumentException>(() => new SpreadsheetSheet("a/b", [new("欄", 10)]));
        Assert.Throws<ArgumentException>(() => new SpreadsheetSheet(new string('名', 32), [new("欄", 10)]));
        var sheet = new SpreadsheetSheet("表", [new("欄", 10)]);
        // 試算表沒有時區：DateTimeOffset 要先由呼叫端換成要顯示的當地時間。
        Assert.Throws<ArgumentException>(() => sheet.Add(DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => sheet.Add("一", "二"));
        Assert.Throws<ArgumentException>(() => Spreadsheet.Write([sheet, new SpreadsheetSheet("表", [new("欄", 10)])]));
    }

    private static SpreadsheetDocument Open(byte[] content) => SpreadsheetDocument.Open(new MemoryStream(content), false);

    private static string Text(Cell cell) => cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? "";

    private static double Number(Cell cell) => double.Parse(cell.CellValue!.Text, System.Globalization.CultureInfo.InvariantCulture);

    private static CellFormat Format(WorkbookPart workbook, Cell cell)
        => workbook.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ElementAt((int)cell.StyleIndex!.Value);

    private static string FormatCode(WorkbookPart workbook, Cell cell)
    {
        var id = Format(workbook, cell).NumberFormatId!.Value;
        return workbook.WorkbookStylesPart!.Stylesheet!.NumberingFormats!.Elements<NumberingFormat>().Single(x => x.NumberFormatId!.Value == id).FormatCode!.Value!;
    }

    private static Font Font(WorkbookPart workbook, Cell cell)
        => workbook.WorkbookStylesPart!.Stylesheet!.Fonts!.Elements<Font>().ElementAt((int)Format(workbook, cell).FontId!.Value);
}
