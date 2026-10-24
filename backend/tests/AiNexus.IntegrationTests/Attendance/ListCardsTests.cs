using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Office.Attendance.Cards;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListCardsTests
{
    [Fact]
    public async Task SummarisesPunchesNoPeriodCoversPerCard()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        var covered = await CreateCardAsync(hr, "E-COVERED");
        await AssignOkAsync(hr, covered.Id, session.Bob, At(10, 1), At(10, 3));
        var named = await CreateCardAsync(hr, "E-NAMED");
        await AssignOkAsync(hr, named.Id, null, At(10, 1), holderName: "王新進");
        await AddPunchesAsync(factory,
            ("E-COVERED", At(10, 2, 9)),             // 持有期間涵蓋：已歸戶
            ("E-COVERED", At(10, 4, 9)),             // 期間結束之後：未歸戶
            ("E-NAMED", At(10, 2, 9)),               // 只有姓名的持有人：已歸戶
            ("E-FREE", At(10, 5, 18)), ("E-FREE", At(10, 2, 9, 30)), ("E-FREE", At(10, 3, 12)));

        var cards = (await hr.GetFromJsonAsync<CardDto[]>(Cards))!.ToDictionary(x => x.CardNumber);
        // 刷卡出現的卡號（E-FREE）與 HR 新增的卡在同一份清單。
        Assert.Equal(["A001", "E-COVERED", "E-FREE", "E-NAMED"], cards.Keys);
        var free = cards["E-FREE"];
        Assert.Equal((3, At(10, 2, 9, 30), At(10, 5, 18), At(10, 5, 18)), (free.UnattributedCount, free.FirstUnattributedAt, free.LastUnattributedAt, free.LastPunchedAt));
        var gap = cards["E-COVERED"];
        Assert.Equal((1, At(10, 4, 9), At(10, 4, 9)), (gap.UnattributedCount, gap.FirstUnattributedAt, gap.LastPunchedAt));
        Assert.Equal((0, (DateTimeOffset?)null, At(10, 2, 9)), (cards["E-NAMED"].UnattributedCount, cards["E-NAMED"].FirstUnattributedAt, cards["E-NAMED"].LastPunchedAt));
        Assert.Equal((0, (DateTimeOffset?)null), (cards["A001"].UnattributedCount, cards["A001"].LastPunchedAt));
    }

    [Fact]
    public async Task FiltersByStatus()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        await AssignOkAsync(hr, session.Card.Id, session.Bob, At(10, 1));
        await CreateCardAsync(hr, "V-GUEST", hidden: true);
        await AddPunchesAsync(factory, ("V-GUEST", At(10, 2, 9)), ("NEW-1", At(10, 3, 9)), ("NEW-2", At(10, 5, 9)));

        async Task<string[]> NumbersAsync(string status) => [.. (await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?status={status}"))!.Select(x => x.CardNumber)];

        // 待指派不含隱藏的卡（例如訪客卡），最近有未歸戶刷卡的排最前面。
        Assert.Equal(["NEW-2", "NEW-1"], await NumbersAsync(CardStatuses.Unassigned));
        Assert.Equal(["A001"], await NumbersAsync(CardStatuses.Held));
        Assert.Equal(["V-GUEST"], await NumbersAsync(CardStatuses.Hidden));
        await AssertCodeAsync(await hr.GetAsync($"{Cards}?status=guest"), HttpStatusCode.BadRequest, "attendance_filter_invalid");
    }

    [Fact]
    public async Task AssigningFromTheFirstUnattributedPunchClearsTheCardFromUnassigned()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr) = (session.Factory, session.Hr);
        await AddPunchesAsync(factory, ("A001", At(10, 2, 8, 50)), ("A001", At(10, 2, 18, 10)), ("A001", At(10, 3, 8, 45)));
        var listed = Assert.Single((await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?status={CardStatuses.Unassigned}"))!);
        Assert.Equal((session.Card.Id, 3), (listed.Id, listed.UnattributedCount));

        var period = await AssignOkAsync(hr, session.Card.Id, session.Bob, listed.FirstUnattributedAt!.Value);
        Assert.Empty((await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?status={CardStatuses.Unassigned}"))!);

        // 期間在最後一筆刷卡之前結束時，之後的刷卡又會變成待指派。
        (await hr.PutAsJsonAsync($"{Assignments}/{period.Id}", new CardAssignmentUpdateRequest(session.Bob, null, period.StartsAt, At(10, 3), null))).EnsureSuccessStatusCode();
        var again = Assert.Single((await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?status={CardStatuses.Unassigned}"))!);
        Assert.Equal((1, At(10, 3, 8, 45)), (again.UnattributedCount, again.FirstUnattributedAt));
    }
}
