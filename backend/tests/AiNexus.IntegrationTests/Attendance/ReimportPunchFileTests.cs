using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Staff.Attendance.Imports;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ReimportPunchFileTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly PunchReimportRequest Request = new(new DateOnly(2026, 10, 1));

    [Fact]
    public async Task HrReimportsTheFileOfAGivenDateEvenWhenItDidNotChange()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await session.Hr.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<PunchImportDto>(TestContext.Current.CancellationToken))!;
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

        var response = await session.Hr.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

        var response = await session.Hr.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken);

        // 檔案存在、匯入本身失敗：請求成功，結果是一筆 failed 的紀錄。
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<PunchImportDto>(TestContext.Current.CancellationToken))!;
        Assert.Equal((PunchImportStatuses.Failed, PunchImportTriggers.Manual, 0), (dto.Status, dto.TriggerKind, dto.PunchCount));
        Assert.Equal(session.HrId, dto.RequestedById);
        Assert.Contains("第 2 行", dto.ErrorMessage);
        Assert.Equal([At(10, 1, 8, 0)], (await session.PunchesAsync()).Select(x => x.At));
    }

    [Fact]
    public async Task AMissingFileOrUnreadableSourceFolderIsAnExpectedFailureWithNoRecord()
    {
        await using var session = await PunchImportSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken), HttpStatusCode.NotFound, "attendance_import_file_not_found");

        Directory.Delete(session.Source.Folder, recursive: true);
        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken), HttpStatusCode.ServiceUnavailable, "attendance_import_source_unavailable");
        Assert.Empty(await session.ImportsAsync());
    }

    [Fact]
    public async Task ADateOutsideAnyPossibleFileIsRejectedByTheValidator()
    {
        await using var session = await PunchImportSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Imports, new PunchReimportRequest(new DateOnly(1999, 12, 31)), TestContext.Current.CancellationToken), HttpStatusCode.BadRequest, "validation_failed");
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

    [Fact]
    public async Task ATestIdentityReadsImportsButCannotReimport()
    {
        using var source = new PunchSource();
        source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await using var factory = source.CreateFactory(administrators: ["alice"]);
        using var admin = factory.CreateClient();
        await Login(admin, "alice");
        var hrId = await CreateUser(admin, LocalAccount());
        await GrantImportsAsync(factory, hrId);
        (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(hrId, "重現人資看到的匯入紀錄"), TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        await Csrf(admin);

        // 匯入紀錄是管理頁：測試身分可以讀取以重現畫面，但不能重匯，否則紀錄會把被扮演的人記成操作者。
        Assert.Empty((await admin.GetFromJsonAsync<PunchImportDto[]>(Imports, TestContext.Current.CancellationToken))!);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Imports, Request, TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "test_admin_read_only");
        Assert.Empty((await admin.GetFromJsonAsync<PunchImportDto[]>(Imports, TestContext.Current.CancellationToken))!);
    }
}
