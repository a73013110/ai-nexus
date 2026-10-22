using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Staff.Attendance.Imports;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListPunchImportsTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly string Oct2 = NameOf(10, 2);

    private static async Task<PunchImportDto[]> ListAsync(HttpClient client, string query = "")
        => (await client.GetFromJsonAsync<PunchImportDto[]>($"{Imports}{query}", TestContext.Current.CancellationToken))!;

    /// <summary>10/1 成功一次、10/2 失敗一次再被 HR 手動重匯成功；每一步隔一分鐘，紀錄的先後才分得出來。</summary>
    private static async Task<PunchImportSession> SessionWithHistoryAsync()
    {
        var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        session.Source.Write(Oct2, [Line("A001", 10, 2, 8, 0), "broken"]);
        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(1));
        session.Source.Write(Oct2, [Line("A001", 10, 2, 8, 0)]);
        await session.Hr.PostAsJsonAsync(Imports, new PunchReimportRequest(new DateOnly(2026, 10, 2)), TestContext.Current.CancellationToken);
        return session;
    }

    [Fact]
    public async Task ListsNewestFirstWithTheFileNameAndTheRequesterName()
    {
        await using var session = await SessionWithHistoryAsync();

        var list = await ListAsync(session.Hr);

        Assert.Equal(3, list.Length);
        Assert.Equal(PunchImportTriggers.Manual, list[0].TriggerKind);
        Assert.Equal((Oct2, session.HrId), (list[0].FileName, list[0].RequestedById));
        Assert.NotNull(list[0].RequestedByName);
        Assert.All(list[1..], x => Assert.Null(x.RequestedById));
        Assert.Equal([Oct1, Oct2], list[1..].Select(x => x.FileName).Order());
        var failed = Assert.Single(list, x => x.Status == PunchImportStatuses.Failed);
        Assert.Equal((Oct2, 0), (failed.FileName, failed.PunchCount));
        Assert.Contains("第 2 行", failed.ErrorMessage);
    }

    [Fact]
    public async Task FiltersByFileStatusAndPeriod()
    {
        await using var session = await SessionWithHistoryAsync();
        var all = await ListAsync(session.Hr);
        var oct2Id = all.First(x => x.FileName == Oct2).PunchFileId;
        var started = session.Clock.GetUtcNow();

        Assert.Equal(2, (await ListAsync(session.Hr, $"?fileName={Oct2}")).Length);
        Assert.Equal(2, (await ListAsync(session.Hr, $"?fileId={oct2Id}")).Length);
        Assert.Empty(await ListAsync(session.Hr, "?fileName=20250101.txt"));
        Assert.Equal([Oct2], (await ListAsync(session.Hr, "?status=failed")).Select(x => x.FileName));
        Assert.Equal(2, (await ListAsync(session.Hr, "?status=succeeded")).Length);
        // 期間依開始時間：手動重匯晚一分鐘，掃描那兩筆在它之前。
        var late = Uri.EscapeDataString(started.AddSeconds(-30).ToString("o"));
        Assert.Equal([PunchImportTriggers.Manual], (await ListAsync(session.Hr, $"?from={late}")).Select(x => x.TriggerKind));
        Assert.Equal(2, (await ListAsync(session.Hr, $"?until={late}")).Length);
    }

    [Fact]
    public async Task PagesWithACursor()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        for (var i = 0; i < 27; i++)
        {
            Assert.True((await session.ReimportAsync(Oct1)).IsSuccess);
            session.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var first = await ListAsync(session.Hr, "?take=25");
        var second = await ListAsync(session.Hr, $"?take=25&before={first[^1].Id}");

        Assert.Equal((25, 2), (first.Length, second.Length));
        Assert.Empty(first.Select(x => x.Id).Intersect(second.Select(x => x.Id)));
        Assert.True(first[^1].StartedAt >= second[0].StartedAt);
        Assert.Equal(27, first.Length + second.Length);
    }

    [Theory]
    [InlineData("?status=bogus")]
    [InlineData("?take=7")]
    [InlineData("?from=2026-10-02T00:00:00%2B08:00&until=2026-10-01T00:00:00%2B08:00")]
    [InlineData("?before=11111111-1111-1111-1111-111111111111")]
    public async Task InvalidFiltersAreRejected(string query)
    {
        await using var session = await PunchImportSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.GetAsync($"{Imports}{query}", TestContext.Current.CancellationToken), HttpStatusCode.BadRequest, "attendance_import_filter_invalid");
    }
}
