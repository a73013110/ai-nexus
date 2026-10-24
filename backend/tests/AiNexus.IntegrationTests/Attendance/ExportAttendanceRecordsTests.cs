using System.Globalization;
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
        session.Gd.AddLeave("L-1", GdId("bob"), "A", new(2026, 10, 19, 8, 30, 0), new(2026, 10, 19, 9, 0, 0), "Finish");
        await session.PunchAsync("A1", At(19, 9, 5), At(19, 18, 0), At(20, 8, 30), At(20, 17, 45), At(21, 8, 20));
        using var client = await session.Factory.SignedInAsync("bob");
        var person = Assert.Single((await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People);

        var response = await client.GetAsync($"{Export}?from=2026-10-19&to=2026-10-21");

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(Spreadsheet.ContentType, response.Content.Headers.ContentType!.MediaType);
        // 中文檔名以 RFC 5987 的 filename* 傳遞；時間是固定時鐘的台灣時間。
        Assert.Equal("出勤紀錄_20261019-20261021_20261022_100000.xlsx", response.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());

        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        Assert.Equal(["出勤紀錄", "統計", "說明"], workbook.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(x => x.Name!.Value));

        var days = Rows(workbook, "出勤紀錄");
        Assert.Equal(["姓名", "帳號或卡號", "日期", "星期", "日型", "行事曆備註", "狀態", "上班", "下班", "應下班", "遲到", "早退", "說明", "假單與出差"], days[0].Select(x => x.Text));
        Assert.Equal(4, days.Count);
        var late = days[1];
        Assert.Equal((person.DisplayName, person.Account, new DateTime(2026, 10, 19), "一", "上班日", "出勤"), (late[0].Text, late[1].Text, DateTime.FromOADate(late[2].Number), late[3].Text, late[4].Text, late[6].Text));
        Assert.Equal((new TimeSpan(9, 5, 0), new TimeSpan(18, 0, 0), new TimeSpan(18, 0, 0), TimeSpan.FromMinutes(5)), (late[7].Time, late[8].Time, late[9].Time, late[10].Time));
        Assert.True(late[7].Danger);
        Assert.False(late[8].Danger);
        Assert.Equal("假單：特休（10/19 08:30–10/19 09:00，Finish，L-1）", late[13].Text);
        var early = days[2];
        Assert.Equal((TimeSpan.FromMinutes(15), true, ""), (early[11].Time, early[8].Danger, early[10].Text));
        // 只有一筆刷卡：異常、列在上班，下班空白，不算遲到早退。
        var single = days[3];
        Assert.Equal(("異常", true, new TimeSpan(8, 20, 0), "", "只有一筆刷卡，分不出上班或下班"), (single[6].Text, single[6].Danger, single[7].Time, single[8].Text, single[12].Text));

        var summary = Rows(workbook, "統計");
        Assert.Equal(2, summary.Count);
        Assert.Equal((person.DisplayName, 3d, 2d, 1d, 1d, TimeSpan.FromMinutes(5), 1d, TimeSpan.FromMinutes(15)),
            (summary[1][0].Text, summary[1][2].Number, summary[1][3].Number, summary[1][6].Number, summary[1][8].Number, summary[1][9].Time, summary[1][10].Number, summary[1][11].Time));

        var notes = Rows(workbook, "說明").Select(x => string.Join("｜", x.Select(c => c.Text))).ToList();
        Assert.Contains("查詢區間｜2026/10/19～2026/10/21（出勤日，台灣時間）", notes);
        Assert.Contains(notes, x => x.StartsWith("上班日｜表定 08:30～18:00，彈性上班 08:00～09:00，應下班＝上班＋9.5 小時", StringComparison.Ordinal));
        Assert.Contains(notes, x => x.StartsWith("遲到｜", StringComparison.Ordinal) && x.Contains("09:10 到，遲到 10 分，應下班 18:00", StringComparison.Ordinal));

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
        await session.PunchAsync("Z9", At(19, 9, 0), At(19, 18, 0));
        using var client = await session.Factory.SignedInAsync("hr");

        // 未歸戶卡號預設不匯出；要求時排在最後，「帳號或卡號」欄寫卡號。
        Assert.Equal(["新進同仁"], await PeopleAsync(client, Week));
        var response = await client.GetAsync($"{Export}?{Week}&includeUnattributed=true");
        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        var rows = Rows(workbook, "出勤紀錄").Skip(1).ToList();
        Assert.Equal([("新進同仁", "A2"), ("卡號 Z9", "Z9")], rows.Select(x => (x[0].Text, x[1].Text)).Distinct());
        Assert.Contains(Rows(workbook, "統計"), x => x[0].Text == "卡號 Z9" && x[^1].Text.Contains("未歸戶卡號"));
        Assert.Contains("\"includeUnattributed\":true", (await AttendanceApi.AuditAsync(session.Factory, "attendance.records.export"))[^1].Details);
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

    /// <summary>匯出檔「出勤紀錄」工作表上不重複的姓名，依出現順序。</summary>
    private static async Task<string[]> PeopleAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Export}?{query}");
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        return [.. Rows(workbook, "出勤紀錄").Skip(1).Select(x => x[0].Text).Distinct()];
    }

    /// <summary>匯出檔「出勤紀錄」工作表的資料列數（每人每天一列）。</summary>
    private static async Task<int> DayCountAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Export}?{query}");
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        return Rows(workbook, "出勤紀錄").Count - 1;
    }

    /// <summary>儲存格的值：文字、數字（日期、時刻、時數是 Excel 的天數）與是否以警示色標示。</summary>
    private sealed record Value(string Text, bool Danger)
    {
        public double Number => double.Parse(Text, CultureInfo.InvariantCulture);
        public TimeSpan Time => TimeSpan.FromSeconds(Math.Round(Number * 86400));
    }

    /// <summary>一張工作表的每一列，依欄位補齊空白儲存格。</summary>
    private static List<Value[]> Rows(SpreadsheetDocument workbook, string name)
    {
        var part = workbook.WorkbookPart!;
        var id = part.Workbook!.Sheets!.Elements<Sheet>().Single(x => x.Name == name).Id!.Value!;
        var worksheet = ((WorksheetPart)part.GetPartById(id)).Worksheet!;
        var width = worksheet.Descendants<Column>().Count();
        var formats = part.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ToList();
        var fonts = part.WorkbookStylesPart.Stylesheet!.Fonts!.Elements<Font>().ToList();
        return [.. worksheet.Descendants<Row>().Select(row =>
        {
            var values = Enumerable.Repeat(new Value("", false), width).ToArray();
            foreach (var cell in row.Elements<Cell>())
            {
                var column = cell.CellReference!.Value!.TakeWhile(char.IsLetter).Aggregate(0, (n, c) => n * 26 + c - 'A' + 1) - 1;
                var font = fonts[(int)formats[(int)(cell.StyleIndex?.Value ?? 0)].FontId!.Value];
                values[column] = new(cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? "", font.Color?.Rgb?.Value == "FFC00000");
            }
            return values;
        })];
    }
}
