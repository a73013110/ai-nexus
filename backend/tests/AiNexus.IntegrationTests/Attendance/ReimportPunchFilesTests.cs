using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Imports;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ReimportPunchFilesTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly string Oct2 = NameOf(10, 2);
    private static readonly string Oct3 = NameOf(10, 3);
    private static readonly string Oct4 = NameOf(10, 4);
    private static readonly PunchReimportRequest Request = Range(10, 1, 10, 1);

    private static PunchReimportRequest Range(int fromMonth, int fromDay, int toMonth, int toDay)
        => new(new DateOnly(2026, fromMonth, fromDay), new DateOnly(2026, toMonth, toDay));

    private static async Task<PunchReimportResult> ReimportOkAsync(HttpClient client, PunchReimportRequest request)
    {
        var response = await client.PostAsJsonAsync(Imports, request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PunchReimportResult>(TestContext.Current.CancellationToken))!;
    }

    /// <summary>驗證失敗時 <c>errors</c> 的欄位與規則代碼（不含輸入值）。</summary>
    private static async Task<Dictionary<string, string[]>> RejectedAsync(HttpClient client, PunchReimportRequest request)
    {
        var response = await client.PostAsJsonAsync(Imports, request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        return body.GetProperty("errors").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public async Task HrReimportsTheFileOfAGivenDateEvenWhenItDidNotChange()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await ReimportOkAsync(session.Hr, Request);

        var dto = Assert.Single(result.Imports);
        Assert.Empty(result.MissingFiles);
        Assert.Equal((Oct1, PunchImportTriggers.Manual, PunchImportStatuses.Succeeded, 2), (dto.FileName, dto.TriggerKind, dto.Status, dto.PunchCount));
        Assert.Equal(session.HrId, dto.RequestedById);
        Assert.NotNull(dto.RequestedByName);
        Assert.Null(dto.ErrorMessage);
        // 紀錄裡操作者就是目前使用者；排程那筆沒有操作者。
        var imports = await session.ImportsAsync(Oct1);
        Assert.Equal([PunchImportTriggers.Scheduled, PunchImportTriggers.Manual], imports.Select(x => x.TriggerKind));
        Assert.Equal([null, session.HrId], imports.Select(x => x.RequestedById));
        Assert.Equal(dto.Id, (await session.FileAsync(Oct1)).LastImportId);
        Assert.Equal(2, (await session.PunchesAsync()).Count);
    }

    [Fact]
    public async Task ReimportingAFileThatWasNeverImportedImportsIt()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.Enabled = false);
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);

        await ReimportOkAsync(session.Hr, Request);

        Assert.Single(await session.PunchesAsync());
    }

    [Fact]
    public async Task ReimportingAFileWithBadContentAnswersWithAFailedImportAndKeepsThePreviousPunches()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(5));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), "oops"]);

        // 檔案存在、匯入本身失敗：請求成功，結果是一筆 failed 的紀錄。
        var dto = Assert.Single((await ReimportOkAsync(session.Hr, Request)).Imports);

        Assert.Equal((PunchImportStatuses.Failed, PunchImportTriggers.Manual, 0), (dto.Status, dto.TriggerKind, dto.PunchCount));
        Assert.Equal(session.HrId, dto.RequestedById);
        Assert.Contains("第 2 行", dto.ErrorMessage);
        Assert.Equal([At(10, 1, 8, 0)], (await session.PunchesAsync()).Select(x => x.At));
    }

    [Fact]
    public async Task ARangeReimportsEveryFileOldestFirstAndListsTheMissingOnes()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.Enabled = false);
        // 10/1 深夜的刷卡也出現在 10/2 的檔：由舊到新處理，它歸 10/1。
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 23, 50)]);
        session.Source.Write(Oct2, [Line("A001", 10, 1, 23, 50), Line("A001", 10, 2, 8, 0)]);
        session.Source.Write(Oct3, [Line("A001", 10, 3, 8, 0), "oops"]);

        var result = await ReimportOkAsync(session.Hr, Range(10, 1, 10, 4));

        Assert.Equal([Oct1, Oct2, Oct3], result.Imports.Select(x => x.FileName));
        Assert.Equal([PunchImportStatuses.Succeeded, PunchImportStatuses.Succeeded, PunchImportStatuses.Failed], result.Imports.Select(x => x.Status));
        Assert.Equal([2, 1, 0], result.Imports.Select(x => x.PunchCount));
        Assert.All(result.Imports, x => Assert.Equal((PunchImportTriggers.Manual, session.HrId), (x.TriggerKind, x.RequestedById)));
        Assert.Equal([Oct4], result.MissingFiles);
        Assert.Equal([Oct1, Oct1, Oct2], (await session.PunchesAsync()).Select(x => x.File));
        Assert.Equal(3, (await session.ImportsAsync()).Count);
    }

    [Fact]
    public async Task AMissingFileOrUnreadableSourceFolderIsAnExpectedFailureWithNoRecord()
    {
        await using var session = await PunchImportSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken), HttpStatusCode.NotFound, "attendance_import_file_not_found");
        // 整段都沒有檔案也是同一個錯誤：什麼都沒做，對話框留著讓 HR 改日期。
        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, Range(10, 1, 10, 7), TestContext.Current.CancellationToken), HttpStatusCode.NotFound, "attendance_import_file_not_found");

        Directory.Delete(session.Source.Folder, recursive: true);
        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, Range(10, 1, 10, 7), TestContext.Current.CancellationToken), HttpStatusCode.ServiceUnavailable, "attendance_import_source_unavailable");
        Assert.Empty(await session.ImportsAsync());
    }

    [Fact]
    public async Task TheValidatorRejectsImpossibleReversedFutureAndOverlongRanges()
    {
        await using var session = await PunchImportSession.CreateAsync();

        Assert.Equal(["range"], (await RejectedAsync(session.Hr, new(new DateOnly(1999, 12, 31), new DateOnly(1999, 12, 31))))["from"]);
        Assert.Equal(["order"], (await RejectedAsync(session.Hr, Range(10, 5, 10, 4)))["to"]);
        // 測試時鐘是台灣 10/22 上午。
        Assert.Equal(["future"], (await RejectedAsync(session.Hr, Range(10, 22, 10, 23)))["to"]);
        Assert.Equal(["too_long"], (await RejectedAsync(session.Hr, Range(9, 21, 10, 22)))["to"]);

        // 31 天（含頭尾）是上限內：通過驗證，只是沒有檔案。
        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, Range(9, 22, 10, 22), TestContext.Current.CancellationToken), HttpStatusCode.NotFound, "attendance_import_file_not_found");
        Assert.Empty(await session.ImportsAsync());
    }

    [Fact]
    public async Task TodayIsTheTaipeiDateNotTheUtcDate()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.Enabled = false);
        // 台灣 10/23 00:30＝UTC 10/22 16:30：10/23 的檔已經可以重匯。
        session.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 23, 0, 30, 0, Taipei));
        var oct23 = NameOf(10, 23);
        session.Source.Write(oct23, [Line("A001", 10, 23, 0, 10)]);

        var result = await ReimportOkAsync(session.Hr, Range(10, 23, 10, 23));

        Assert.Equal([oct23], result.Imports.Select(x => x.FileName));
    }

    [Fact]
    public async Task ReimportNeedsItsOwnGrantAndTheImportsPageAloneOnlyReads()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await GrantImportsAsync(session.Factory, await UserIdAsync(session.Factory, "viewer"), reimport: false);
        using var viewer = await session.Factory.SignedInAsync("viewer");
        using var member = await session.Factory.SignedInAsync("bob");

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Imports, TestContext.Current.CancellationToken)).StatusCode);
        await AssertCodeAsync(await viewer.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.GetAsync(Imports, TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "feature_forbidden");
        Assert.Empty(await session.ImportsAsync());
        Assert.Empty(await session.PunchesAsync());
    }
}
