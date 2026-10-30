using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Office.Attendance.Imports;
using AiNexus.Features.Office.Attendance.Punches;
using AiNexus.Platform.Data.Tables;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListPunchImportPunchesTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly string Oct2 = NameOf(10, 2);

    private static async Task<PunchImportDto[]> ImportsAsync(HttpClient client)
        => [.. (await client.GetFromJsonAsync<TablePage<PunchImportDto>>(Imports, TestContext.Current.CancellationToken))!.Items];

    private static Task<HttpResponseMessage> PunchesAsync(HttpClient client, Guid importId)
        => client.GetAsync($"{Imports}/{importId}/punches", TestContext.Current.CancellationToken);

    private static async Task<TablePage<PunchDto>> PageAsync(HttpClient client, Guid importId)
        => (await (await PunchesAsync(client, importId)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<TablePage<PunchDto>>(TestContext.Current.CancellationToken))!;

    [Fact]
    public async Task ListsThePunchesTheImportWroteOldestFirst()
    {
        await using var session = await PunchImportSession.CreateAsync();
        // 檔案依卡號排列、含前一天深夜的刷卡；回傳依刷卡時間排序。
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 9, 30, 23, 50), Line("B002", 10, 1, 18, 5, machine: "002", eventCode: "12")]);
        await session.ScanAsync();
        var import = Assert.Single(await ImportsAsync(session.Hr));

        var page = await PageAsync(session.Hr, import.Id);

        Assert.True(import.IsCurrent);
        Assert.True(page.Complete);
        Assert.Equal(["A001", "A001", "B002"], page.Items.Select(x => x.CardNumber));
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 23, 50, 0, TimeSpan.FromHours(8)), page.Items[0].PunchedAt);
        Assert.Equal(("002", "12"), (page.Items[2].MachineCode, page.Items[2].EventCode));
        // 沒有登錄持有期間：卡號匯入時自動建立，但刷卡尚未歸戶。
        Assert.All(page.Items, x => Assert.True(x.Unattributed && x.Holder is null));
    }

    [Fact]
    public async Task LeavesOutPunchesOwnedByANeighbouringFile()
    {
        await using var session = await PunchImportSession.CreateAsync();
        // 10/1 深夜的刷卡也出現在 10/2 的檔，歸先匯入的 10/1。
        session.Source.Write(Oct1, [Line("A001", 10, 1, 23, 50)]);
        session.Source.Write(Oct2, [Line("A001", 10, 1, 23, 50), Line("A001", 10, 2, 8, 0)]);
        await session.ScanAsync();
        var oct2 = Assert.Single(await ImportsAsync(session.Hr), x => x.FileName == Oct2);

        var page = await PageAsync(session.Hr, oct2.Id);

        Assert.Equal(oct2.PunchCount, page.Items.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.FromHours(8)), Assert.Single(page.Items).PunchedAt);
    }

    [Fact]
    public async Task OnlyTheLatestSuccessfulImportOfTheFileListsPunches()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(1));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), "broken"]);
        await session.ReimportAsync(Oct1);
        // 失敗不取代上次成功的內容：第一次匯入仍是目前內容。
        var afterFailure = await ImportsAsync(session.Hr);
        Assert.Equal([false, true], afterFailure.Select(x => x.IsCurrent));
        session.Clock.Advance(TimeSpan.FromMinutes(1));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        await session.ReimportAsync(Oct1);

        var imports = await ImportsAsync(session.Hr);

        Assert.Equal([true, false, false], imports.Select(x => x.IsCurrent));
        Assert.Equal(2, (await PageAsync(session.Hr, imports[0].Id)).Items.Count);
        foreach (var replaced in imports[1..])
            await AssertCodeAsync(await PunchesAsync(session.Hr, replaced.Id), HttpStatusCode.Conflict, "attendance_import_not_current");
    }

    [Fact]
    public async Task AnUnknownImportIsNotFound()
    {
        await using var session = await PunchImportSession.CreateAsync();

        await AssertCodeAsync(await PunchesAsync(session.Hr, Guid.NewGuid()), HttpStatusCode.NotFound, "attendance_import_not_found");
    }
}
