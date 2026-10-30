using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Office.Attendance.Punches;
using AiNexus.Platform.Data.Tables;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListCardPunchesTests
{
    private static async Task<TablePage<PunchDto>> PageAsync(HttpClient client, Guid cardId, string query = "")
        => (await client.GetFromJsonAsync<TablePage<PunchDto>>($"{Cards}/{cardId}/punches{query}", TestContext.Current.CancellationToken))!;

    [Fact]
    public async Task ListsTheCardsPunchesNewestFirstWithTheHolderAtTheTime()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        await AssignOkAsync(hr, session.Card.Id, session.Bob, At(10, 2), At(10, 4));
        await AssignOkAsync(hr, session.Card.Id, null, At(10, 4), holderName: "王新進");
        await AddPunchesAsync(factory,
            ("A001", At(10, 1, 9)),   // 第一段期間之前：未歸戶
            ("A001", At(10, 3, 9)),   // bob
            ("A001", At(10, 5, 9)),   // 只有姓名的持有人
            ("B002", At(10, 3, 9)));  // 別張卡

        var page = await PageAsync(hr, session.Card.Id);
        var rows = page.Items;

        Assert.True(page.Complete);
        Assert.Equal([At(10, 5, 9), At(10, 3, 9), At(10, 1, 9)], rows.Select(x => x.PunchedAt));
        Assert.Equal([("王新進", false), ("bob", false), (null, true)], rows.Select(x => (x.Holder, x.Unattributed)));
        Assert.Equal(session.Bob, rows[1].UserId);
    }

    [Fact]
    public async Task SortsByTheRequestedColumn()
    {
        await using var session = await HrSession.CreateAsync();
        await AddPunchesAsync(session.Factory, ("A001", At(10, 3, 9)), ("A001", At(10, 1, 9)), ("A001", At(10, 2, 9)));

        var page = await PageAsync(session.Hr, session.Card.Id, "?sort=punchedAt");

        Assert.Equal([At(10, 1, 9), At(10, 2, 9), At(10, 3, 9)], page.Items.Select(x => x.PunchedAt));
    }

    [Fact]
    public async Task RejectsAnUnknownCard()
    {
        await using var session = await HrSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.GetAsync($"{Cards}/{Guid.NewGuid()}/punches"), HttpStatusCode.NotFound, "attendance_card_not_found");
    }
}
