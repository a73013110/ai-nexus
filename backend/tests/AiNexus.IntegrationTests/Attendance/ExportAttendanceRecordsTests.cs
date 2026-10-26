using System.Net;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Platform.Export;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>出勤紀錄匯出 Excel：與查詢同一組條件、資料範圍與判定，先寫讀取稽核再產生檔案。</summary>
public sealed class ExportAttendanceRecordsTests
{
    private const string Export = Records + "/export";

    [Fact]
    public async Task TheWorkbookListsTheSameJudgementsAsTheQueryAndTheExportIsAudited()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        session.Gd.AddKind("A", "特休");
        session.Gd.AddLeave("L-1", GdId("bob"), "A", new(2026, 10, 19, 8, 30, 0), new(2026, 10, 19, 9, 0, 0), "Decide");
        session.Gd.AddLeave("L-2", GdId("bob"), "A", new(2026, 10, 20, 17, 45, 0), new(2026, 10, 20, 18, 0, 0), "Send");
        await session.PunchAsync("A1", At(19, 9, 5), At(19, 18, 5), At(20, 8, 30), At(20, 17, 45), At(21, 8, 20));
        using var client = await session.Factory.SignedInAsync("bob");
        var person = Assert.Single((await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People);

        var response = await client.GetAsync($"{Export}?from=2026-10-19&to=2026-10-21");

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(Spreadsheet.ContentType, response.Content.Headers.ContentType!.MediaType);
        // 檔名與舊系統相同；中文檔名以 RFC 5987 的 filename* 傳遞，時間是固定時鐘的台灣時間。
        Assert.Equal("出缺勤計算_20261022_100000(20261019to20261021).xlsx", response.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());

        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        Assert.Equal(["出缺勤計算"], workbook.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(x => x.Name!.Value));

        // 與舊系統逐格相同：前兩欄是姓名與卡號、下方三列是小計；遲到的上班與早退的下班紅字；只有一筆刷卡是異常、只寫在上班。
        // 記號：核准的假補足寫「補足」（遲到 5 分、請 30 分、晚走 5 分）、簽核中的單才補足寫「簽核中」、未補足照舊「＊」。
        var rows = Rows(workbook);
        Assert.Equal(["", "", "日期", "上班打卡", "下班打卡", "遲到", "早退", "異常", "備註"], rows[0].Select(x => x.Text));
        Assert.Equal(
        [
            [person.DisplayName, "A1", "2026/10/19 星期一", "09:05:00", "18:05:00", "補足", "", "", "【核准】【特休】L-1〔2026-10-19 08:30:00 ~ 2026-10-19 09:00:00〕"],
            ["遲到(計)", "1", "2026/10/20 星期二", "08:30:00", "17:45:00", "", "簽核中", "", "【流程中】【特休】L-2〔2026-10-20 17:45:00 ~ 2026-10-20 18:00:00〕"],
            ["早退(計)", "1", "2026/10/21 星期三", "08:20:00", "", "", "", "＊", ""],
            // 不到四天時補一列放「異常(計)」，之後每人空兩列。
            ["異常(計)", "1", "", "", "", "", "", "", ""],
            ["", "", "", "", "", "", "", "", ""],
            ["", "", "", "", "", "", "", "", ""]
        ], rows.Skip(1).Select(x => x.Select(c => c.Text).ToArray()));
        Assert.Equal((true, false, false, true), (rows[1][3].Red, rows[1][4].Red, rows[2][3].Red, rows[2][4].Red));

        // 版面照舊檔：標楷體 13.875 點、列高 19.5、欄寬與對齊（備註靠左、其餘置中），沒有凍結窗格與篩選。
        var sheet = Worksheet(workbook);
        Assert.Equal([13.9296875, 7.9296875, 24.9296875, 13.9296875, 13.9296875, 7.9296875, 7.9296875, 7.9296875, 35.9296875],
            sheet.Descendants<Column>().Select(x => x.Width!.Value));
        Assert.All(sheet.Descendants<Row>(), x => Assert.Equal((19.5, true), (x.Height!.Value, x.CustomHeight!.Value)));
        Assert.Empty(sheet.Descendants<Pane>());
        Assert.Empty(sheet.Descendants<AutoFilter>());
        Assert.All(rows.Skip(1).Take(4).SelectMany(x => x), x => Assert.Equal(("標楷體", 13.875), (x.Font, x.Size)));
        Assert.Equal(["center", "center", "center", "center", "center", "center", "center", "center", "left"], rows[1].Select(x => x.Alignment));

        var audit = Assert.Single(await AttendanceApi.AuditAsync(session.Factory, "attendance.records.export"));
        Assert.Equal("read", audit.Result);
        Assert.Contains("\"from\":\"2026-10-19\"", audit.Details);
        Assert.Contains("\"count\":1", audit.Details);
        Assert.Contains("\"includeDaysOff\":false", audit.Details);
    }

    [Fact]
    public async Task TheExportIsNotPagedAndListsHolidaysOnlyWhenAskedTo()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        await session.GrantAsync(hr, DataScopes.All);
        foreach (var (account, card) in new[] { ("bob", "A1"), ("carol", "A2"), ("dave", "A3") })
            await session.HoldAsync(await session.PersonAsync(account), card);
        using var client = await session.Factory.SignedInAsync("hr");

        // 畫面一頁一人，匯出仍是整份名單。
        Assert.Single((await GetAsync(client, Week + "&limit=1")).People);
        Assert.Equal(3, (await PeopleAsync(client, Week)).Length);
        // 週末沒有刷卡也沒有假單：預設不列，要求才列。
        Assert.Equal(3 * 4, await DayCountAsync(client, "from=2026-10-17&to=2026-10-22"));
        Assert.Equal(3 * 6, await DayCountAsync(client, "from=2026-10-17&to=2026-10-22&includeDaysOff=true"));
    }

    [Fact]
    public async Task DataWithoutAnAccountIsExportedUnderItsCardNumber()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        await session.GrantAsync(hr, DataScopes.All);
        await session.HoldAsync(null, "A2");
        await session.PunchAsync("A2", At(19, 9, 0), At(19, 18, 0));
        using var client = await session.Factory.SignedInAsync("hr");

        // 只有姓名的持有人：第二欄寫它依附的卡號；對不到公文帳號，備註寫表單無法取得。
        var rows = await ExportAsync(client, Week);
        Assert.Equal(("新進同仁", "A2"), (rows[1][0].Text, rows[1][1].Text));
        Assert.All(DayRows(rows), x => Assert.Equal("無法取得", x[8].Text));
    }

    [Fact]
    public async Task OnlyPeopleInTheViewersScopeCanBeExported()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        await session.GrantAsync(hr, DataScopes.All);
        using var hrClient = await session.Factory.SignedInAsync("hr");
        using var bobClient = await session.Factory.SignedInAsync("bob");

        // 匯出的人與同條件查詢看到的人相同（順序也相同）：HR 是全公司，bob 只有自己。
        foreach (var client in new[] { hrClient, bobClient })
            Assert.Equal((await GetAsync(client, Week)).People.Select(x => x.DisplayName), await PeopleAsync(client, Week));
        Assert.Equal(2, (await PeopleAsync(hrClient, Week)).Length);
        // 指定範圍外的人只是匯出空表，與查詢相同，不透露他有沒有出勤資料。
        Assert.Empty(await PeopleAsync(bobClient, Week + $"&userId={carol}"));
    }

    [Fact]
    public async Task InvalidRangesAndUsersWithoutTheRecordsPageAreRefusedWithoutAnAudit()
    {
        await using var session = Create();
        using var client = await session.Factory.SignedInAsync("bob");

        foreach (var query in new[] { "from=2026-10-01", "from=2026-10-31&to=2026-10-01", "from=2025-10-01&to=2026-10-02" })
            await AssertCodeAsync(await client.GetAsync($"{Export}?{query}"), HttpStatusCode.BadRequest, "attendance_records_range_invalid");

        await session.WithDbAsync(db => db.Set<RoleGroupFeature>()
            .Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords).ExecuteDeleteAsync());
        await AssertCodeAsync(await client.GetAsync($"{Export}?{Week}"), HttpStatusCode.Forbidden, "feature_forbidden");
        Assert.Empty(await AttendanceApi.AuditAsync(session.Factory, "attendance.records.export"));
    }

    /// <summary>匯出檔每段開頭的姓名，依出現順序。</summary>
    private static async Task<string[]> PeopleAsync(HttpClient client, string query)
    {
        var rows = await ExportAsync(client, query);
        return [.. rows.Skip(1).Select(x => x[0].Text).Where(x => x != "" && !x.EndsWith("(計)", StringComparison.Ordinal))];
    }

    /// <summary>匯出檔的出勤日列數（每人每天一列，不含小計補列與段間空列）。</summary>
    private static async Task<int> DayCountAsync(HttpClient client, string query) => DayRows(await ExportAsync(client, query)).Count();

    private static IEnumerable<Value[]> DayRows(List<Value[]> rows) => rows.Skip(1).Where(x => x[2].Text != "");

    private static async Task<List<Value[]>> ExportAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Export}?{query}");
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        return Rows(workbook);
    }

    /// <summary>儲存格的文字（數字照存值）、是否紅字，以及字型、字級與水平對齊。</summary>
    private sealed record Value(string Text, bool Red, string Font, double Size, string Alignment);

    private static Worksheet Worksheet(SpreadsheetDocument workbook)
    {
        var part = workbook.WorkbookPart!;
        var id = part.Workbook!.Sheets!.Elements<Sheet>().Single().Id!.Value!;
        return ((WorksheetPart)part.GetPartById(id)).Worksheet!;
    }

    /// <summary>工作表的每一列，依欄位補齊空白儲存格。</summary>
    private static List<Value[]> Rows(SpreadsheetDocument workbook)
    {
        var worksheet = Worksheet(workbook);
        var width = worksheet.Descendants<Column>().Count();
        var styles = workbook.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        var formats = styles.CellFormats!.Elements<CellFormat>().ToList();
        var fonts = styles.Fonts!.Elements<Font>().ToList();
        return [.. worksheet.Descendants<Row>().Select(row =>
        {
            var values = Enumerable.Repeat(new Value("", false, "", 0, ""), width).ToArray();
            foreach (var cell in row.Elements<Cell>())
            {
                var column = cell.CellReference!.Value!.TakeWhile(char.IsLetter).Aggregate(0, (n, c) => n * 26 + c - 'A' + 1) - 1;
                var format = formats[(int)(cell.StyleIndex?.Value ?? 0)];
                var font = fonts[(int)format.FontId!.Value];
                values[column] = new(cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? "", font.Color?.Rgb?.Value == "FFFF0000",
                    font.GetFirstChild<FontName>()!.Val!.Value!, font.GetFirstChild<FontSize>()!.Val!.Value,
                    format.Alignment?.Horizontal?.ToString() ?? "");
            }
            return values;
        })];
    }
}
