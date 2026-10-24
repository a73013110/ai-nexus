using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Office.Attendance.Cards;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListCardPunchesTests
{
    /// <summary>與 <c>ListCardPunches.PageSize</c> 相同。</summary>
    private const int PageSize = 50;

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

        var rows = (await hr.GetFromJsonAsync<CardPunchDto[]>($"{Cards}/{session.Card.Id}/punches"))!;

        Assert.Equal([At(10, 5, 9), At(10, 3, 9), At(10, 1, 9)], rows.Select(x => x.PunchedAt));
        Assert.Equal([("王新進", false), ("bob", false), (null, true)], rows.Select(x => (x.Holder, x.Unattributed)));
        Assert.Equal(session.Bob, rows[1].UserId);
    }

    [Fact]
    public async Task PagesWithTheLastPunchAsCursor()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        var times = Enumerable.Range(0, PageSize + 3).Select(i => At(10, 1, 7).AddMinutes(i)).ToArray();
        await AddPunchesAsync(factory, [.. times.Select(x => ("A001", x))]);

        var first = (await hr.GetFromJsonAsync<CardPunchDto[]>($"{Cards}/{session.Card.Id}/punches"))!;
        var second = (await hr.GetFromJsonAsync<CardPunchDto[]>($"{Cards}/{session.Card.Id}/punches?before={first[^1].Id}"))!;

        Assert.Equal(PageSize, first.Length);
        Assert.Equal(times.Reverse().Skip(PageSize), second.Select(x => x.PunchedAt));
    }

    [Fact]
    public async Task RejectsAnUnknownCardOrACursorFromAnotherCard()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        var card = await CreateCardAsync(hr, "B002");
        await AddPunchesAsync(factory, ("B002", At(10, 3, 9)));
        var other = Assert.Single((await hr.GetFromJsonAsync<CardPunchDto[]>($"{Cards}/{card.Id}/punches"))!);

        await AssertCodeAsync(await hr.GetAsync($"{Cards}/{Guid.NewGuid()}/punches"), HttpStatusCode.NotFound, "attendance_card_not_found");
        await AssertCodeAsync(await hr.GetAsync($"{Cards}/{session.Card.Id}/punches?before={other.Id}"), HttpStatusCode.BadRequest, "attendance_filter_invalid");
    }
}
