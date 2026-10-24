using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Cards;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListUnattributedCardsTests
{
    [Fact]
    public async Task ListsOnlyEmployeeCardPunchesNoPeriodCoversSummarisedByCard()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        var covered = await CreateCardAsync(hr, "E-COVERED");
        await AssignOkAsync(hr, covered.Id, session.Bob, At(10, 1), At(10, 3));
        var named = await CreateCardAsync(hr, "E-NAMED");
        await AssignOkAsync(hr, named.Id, null, At(10, 1), holderName: "王新進");
        await CreateCardAsync(hr, "E-FREE");
        await CreateCardAsync(hr, "V-GUEST", CardKinds.Visitor);
        await AddPunchesAsync(factory,
            ("E-COVERED", At(10, 2, 9)),             // 持有期間涵蓋：已歸戶
            ("E-COVERED", At(10, 4, 9)),             // 期間結束之後：未歸戶
            ("E-NAMED", At(10, 2, 9)),               // 只有姓名的持有人：已歸戶
            ("V-GUEST", At(10, 2, 9)),               // 訪客卡：不歸戶也不算未歸戶
            ("E-FREE", At(10, 5, 18)), ("E-FREE", At(10, 2, 9, 30)), ("E-FREE", At(10, 3, 12)),
            ("UNREGISTERED", At(10, 3, 10)));        // 沒有登錄的卡號也算未歸戶

        var list = (await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!;
        // 最近有刷卡的排最前面。
        Assert.Equal(["E-FREE", "E-COVERED", "UNREGISTERED"], list.Select(x => x.CardNumber));
        var free = list[0];
        Assert.Equal((3, At(10, 2, 9, 30), At(10, 5, 18)), (free.PunchCount, free.FirstPunchedAt, free.LastPunchedAt));
        Assert.NotNull(free.CardId);
        Assert.Equal((1, At(10, 4, 9)), (list[1].PunchCount, list[1].FirstPunchedAt));
        Assert.Null(list[2].CardId);
    }

    [Fact]
    public async Task FiltersByAttendanceDateInclusive()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        await AddPunchesAsync(factory, ("A001", At(10, 2, 9)), ("A001", At(10, 5, 18)), ("A002", At(10, 3, 23, 59)), ("A003", At(10, 6, 0)));

        async Task<string[]> NumbersAsync(string query) => [.. (await hr.GetFromJsonAsync<UnattributedCardDto[]>($"{Unattributed}{query}"))!.Select(x => x.CardNumber)];

        Assert.Equal(["A003", "A001", "A002"], await NumbersAsync(""));
        Assert.Equal(["A001", "A002"], await NumbersAsync("?to=2026-10-05"));
        Assert.Equal(["A002"], await NumbersAsync("?from=2026-10-03&to=2026-10-03"));
        Assert.Equal(["A003"], await NumbersAsync("?from=2026-10-06"));
        var summary = Assert.Single((await hr.GetFromJsonAsync<UnattributedCardDto[]>($"{Unattributed}?from=2026-10-03"))!, x => x.CardNumber == "A001");
        // 期間只篩選要彙總的刷卡：10/2 的那筆不算進筆數與第一筆。
        Assert.Equal((1, At(10, 5, 18)), (summary.PunchCount, summary.FirstPunchedAt));
        await AssertCodeAsync(await hr.GetAsync($"{Unattributed}?from=2026-10-06&to=2026-10-05"), HttpStatusCode.BadRequest, "attendance_period_invalid");
    }

    [Fact]
    public async Task AssigningFromTheFirstUnattributedPunchClearsTheCardFromTheList()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        await AddPunchesAsync(factory, ("A001", At(10, 2, 8, 50)), ("A001", At(10, 2, 18, 10)), ("A001", At(10, 3, 8, 45)));
        var listed = Assert.Single((await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!);
        Assert.Equal(session.Card.Id, listed.CardId);

        await AssignOkAsync(hr, session.Card.Id, session.Bob, listed.FirstPunchedAt);

        Assert.Empty((await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!);
        // 持有期間在最後一筆刷卡之前結束時，之後的刷卡又會回到清單，且第一筆是結束之後的那筆。
        var history = Assert.Single(await HistoryAsync(hr, session.Card.Id));
        (await hr.PutAsJsonAsync($"{Assignments}/{history.Id}", new CardAssignmentUpdateRequest(session.Bob, null, listed.FirstPunchedAt, At(10, 3), null))).EnsureSuccessStatusCode();
        var again = Assert.Single((await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!);
        Assert.Equal((1, At(10, 3, 8, 45)), (again.PunchCount, again.FirstPunchedAt));
    }
}
