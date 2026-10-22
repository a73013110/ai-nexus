using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Staff.Attendance.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class AssignCardTests
{
    [Fact]
    public async Task PeriodsOfTheSameCardCannotOverlapButTouchingPeriodsCan()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, carol, card) = (session.Factory, session.Hr, session.Bob, session.Carol, session.Card);
        await AssignOkAsync(hr, card.Id, bob, At(10, 1), At(10, 10));

        // 落在既有期間中間：接手也會把它切成兩段，所以不論有沒有旗標都拒絕。
        foreach (var takeOver in new[] { false, true })
            await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 5), At(10, 8), takeOver), HttpStatusCode.Conflict, "attendance_assignment_overlap");
        // 結束在前一段結束之前的新段也一樣（新段的尾端沒有蓋過舊段）。
        await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 9), At(10, 9, 12), takeOver: true), HttpStatusCode.Conflict, "attendance_assignment_overlap");
        Assert.Single(await HistoryAsync(hr, card.Id));

        // 首尾相接不算重疊；同一個人可以同時持有多張卡。
        await AssignOkAsync(hr, card.Id, carol, At(10, 10), At(10, 20));
        var second = await CreateCardAsync(hr, "A002");
        await AssignOkAsync(hr, second.Id, bob, At(10, 1));
        Assert.Equal(2, (await hr.GetFromJsonAsync<CardAssignmentDto[]>($"{Assignments}?userId={bob}"))!.Length);
        Assert.Equal([carol, bob], (await HistoryAsync(hr, card.Id)).Select(x => x.UserId));
    }

    [Fact]
    public async Task UpdatingAPeriodIntoAnotherOneIsRejected()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, carol, card) = (session.Factory, session.Hr, session.Bob, session.Carol, session.Card);
        var first = await AssignOkAsync(hr, card.Id, bob, At(10, 1), At(10, 10));
        var second = await AssignOkAsync(hr, card.Id, carol, At(10, 10));

        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Assignments}/{second.Id}", new CardAssignmentUpdateRequest(carol, null, At(10, 9), null, null)), HttpStatusCode.Conflict, "attendance_assignment_overlap");
        // 更正自己的期間不與自己比較；結束持有就是填上結束時間。
        (await hr.PutAsJsonAsync($"{Assignments}/{first.Id}", new CardAssignmentUpdateRequest(bob, null, At(10, 1), At(10, 9), "提早離職"))).EnsureSuccessStatusCode();
        (await hr.PutAsJsonAsync($"{Assignments}/{second.Id}", new CardAssignmentUpdateRequest(carol, null, At(10, 9), At(10, 30), null))).EnsureSuccessStatusCode();
        var history = await HistoryAsync(hr, card.Id);
        Assert.Equal([At(10, 30), At(10, 9)], history.Select(x => x.EndsAt));
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Assignments}/{Guid.NewGuid()}", new CardAssignmentUpdateRequest(bob, null, At(11, 1), null, null)), HttpStatusCode.NotFound, "attendance_assignment_not_found");
    }

    [Fact]
    public async Task AssigningAHeldCardNeedsTheTakeOverFlagAndTheHistoryNamesTheHolder()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, carol, card) = (session.Factory, session.Hr, session.Bob, session.Carol, session.Card);
        var current = await AssignOkAsync(hr, card.Id, bob, At(10, 1));

        await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 5)), HttpStatusCode.Conflict, "attendance_card_held");
        // 錯誤回應不帶資料；前端從持有歷史提示目前是誰持有。被拒絕時什麼都沒變。
        var unchanged = Assert.Single(await HistoryAsync(hr, card.Id));
        Assert.Equal((current.Id, bob, "bob", (DateTimeOffset?)null), (unchanged.Id, unchanged.UserId, unchanged.UserDisplayName, unchanged.EndsAt));

        var taken = await AssignOkAsync(hr, card.Id, carol, At(10, 5), takeOver: true);
        Assert.Null(taken.EndsAt);
        var history = await HistoryAsync(hr, card.Id);
        Assert.Equal([carol, bob], history.Select(x => x.UserId));
        // 前一段結束在新段開始的那一刻，兩段首尾相接、沒有空檔也沒有重疊。
        Assert.Equal(At(10, 5), history[1].EndsAt);
        Assert.Equal(At(10, 5), history[0].StartsAt);

        // 沒有人持有的時段（舊段已結束）不需要旗標。
        var other = await CreateCardAsync(hr, "A003");
        await AssignOkAsync(hr, other.Id, bob, At(10, 1), At(10, 3));
        await AssignOkAsync(hr, other.Id, carol, At(10, 4));
    }

    [Fact]
    public async Task TakeOverOfAClosedPeriodThatTheNewOneOutlastsEndsItAtTheNewStart()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, carol, card) = (session.Factory, session.Hr, session.Bob, session.Carol, session.Card);
        await AssignOkAsync(hr, card.Id, bob, At(10, 1), At(10, 10));

        await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 5)), HttpStatusCode.Conflict, "attendance_card_held");
        await AssignOkAsync(hr, card.Id, carol, At(10, 5), At(10, 12), takeOver: true);
        Assert.Equal([At(10, 12), At(10, 5)], (await HistoryAsync(hr, card.Id)).Select(x => x.EndsAt));
    }

    [Fact]
    public async Task NewPeriodsStartingNoLaterThanThePreviousOneAreRejectedWithOrWithoutTheFlag()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, carol, card) = (session.Factory, session.Hr, session.Bob, session.Carol, session.Card);
        await AssignOkAsync(hr, card.Id, bob, At(10, 5));

        foreach (var takeOver in new[] { false, true })
        {
            await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 5), takeOver: takeOver), HttpStatusCode.Conflict, "attendance_assignment_starts_not_after_previous");
            await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 1), takeOver: takeOver), HttpStatusCode.Conflict, "attendance_assignment_starts_not_after_previous");
            await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 1), At(10, 6), takeOver), HttpStatusCode.Conflict, "attendance_assignment_starts_not_after_previous");
        }
        var only = Assert.Single(await HistoryAsync(hr, card.Id));
        Assert.Equal((bob, null), (only.UserId, only.EndsAt));
        // 完全在前一段之前、沒有重疊的補登仍可以。
        await AssignOkAsync(hr, card.Id, carol, At(9, 1), At(9, 20));
    }

    [Fact]
    public async Task AHolderKnownOnlyByNameCanBeBoundToAUserLater()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, card) = (session.Factory, session.Hr, session.Bob, session.Card);
        var dave = await UserIdAsync(factory, "dave");
        var named = await AssignOkAsync(hr, card.Id, null, At(10, 1), holderName: "  王新進  ");
        Assert.Equal((null, "王新進"), (named.UserId, named.HolderName));
        await AddPunchesAsync(factory, ("A001", At(10, 2, 8, 55)), ("A001", At(10, 3, 18, 5)));

        // 只有姓名也算已歸戶：還沒有帳號的人，刷卡不會出現在未歸戶清單。
        Assert.Empty((await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!);
        Assert.Equal([(Guid?)null, null], await HolderIdsAsync(factory));

        (await hr.PutAsJsonAsync($"{Assignments}/{named.Id}", new CardAssignmentUpdateRequest(dave, "王新進", At(10, 1), null, null))).EnsureSuccessStatusCode();
        var bound = Assert.Single(await HistoryAsync(hr, card.Id));
        Assert.Equal((dave, "dave", "王新進"), (bound.UserId, bound.UserDisplayName, bound.HolderName));
        // 先前的刷卡隨之歸戶，不必改刷卡資料。
        Assert.Equal([(Guid?)dave, dave], await HolderIdsAsync(factory));
        Assert.Equal([bound.Id], (await hr.GetFromJsonAsync<CardAssignmentDto[]>($"{Assignments}?userId={dave}"))!.Select(x => x.Id));
        Assert.Empty((await hr.GetFromJsonAsync<CardAssignmentDto[]>($"{Assignments}?userId={bob}"))!);

        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Assignments}/{named.Id}", new CardAssignmentUpdateRequest(Guid.NewGuid(), null, At(10, 1), null, null)), HttpStatusCode.NotFound, "attendance_user_not_found");
    }

    [Fact]
    public async Task ConcurrentAssignmentsOfOneCardNeverOverlap()
    {
        await using var session = await HrSession.CreateAsync();
        var hr = session.Hr;
        var users = new List<Guid>();
        for (var i = 0; i < 6; i++) users.Add(await UserIdAsync(session.Factory, $"user{i}"));

        // 同一個開始時間同時送出：串行化之後只有第一個成功，其餘看到它的結果而被拒絕，而不是雙雙通過造成重疊。
        var responses = await Task.WhenAll(users.Select(user => AssignAsync(hr, session.Card.Id, user, At(10, 1), takeOver: true)));

        Assert.Equal(1, responses.Count(x => x.IsSuccessStatusCode));
        Assert.All(responses.Where(x => !x.IsSuccessStatusCode), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Single(await HistoryAsync(hr, session.Card.Id));

        // 起始時間不同、都帶接手旗標：每個都成功，但結果是首尾相接的一串，任何時刻只有一人持有。
        var chain = await Task.WhenAll(users.Select((user, i) => AssignAsync(hr, session.Card.Id, user, At(10, 2 + i), takeOver: true)));
        Assert.All(chain, x => Assert.True(x.IsSuccessStatusCode || x.StatusCode == HttpStatusCode.Conflict));
        var history = (await HistoryAsync(hr, session.Card.Id)).OrderBy(x => x.StartsAt).ToList();
        for (var i = 1; i < history.Count; i++) Assert.True(history[i - 1].EndsAt <= history[i].StartsAt, $"{history[i - 1].EndsAt} overlaps {history[i].StartsAt}");
        Assert.Equal(1, history.Count(x => x.EndsAt is null));
    }

    [Fact]
    public async Task AssignmentsNeedAHolderAValidPeriodAnExistingEmployeeCardAndAnExistingUser()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, card) = (session.Factory, session.Hr, session.Bob, session.Card);
        var visitor = await CreateCardAsync(hr, "V001", CardKinds.Visitor);

        Assert.Equal(HttpStatusCode.BadRequest, (await AssignAsync(hr, card.Id, null, At(10, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AssignAsync(hr, card.Id, null, At(10, 1), holderName: "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AssignAsync(hr, card.Id, bob, At(10, 1), At(10, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AssignAsync(hr, card.Id, bob, At(10, 2), At(10, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AssignAsync(hr, card.Id, bob, default)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AssignAsync(hr, Guid.Empty, bob, At(10, 1))).StatusCode);
        await AssertCodeAsync(await AssignAsync(hr, Guid.NewGuid(), bob, At(10, 1)), HttpStatusCode.NotFound, "attendance_card_not_found");
        await AssertCodeAsync(await AssignAsync(hr, card.Id, Guid.NewGuid(), At(10, 1)), HttpStatusCode.NotFound, "attendance_user_not_found");
        await AssertCodeAsync(await AssignAsync(hr, visitor.Id, bob, At(10, 1)), HttpStatusCode.Conflict, "attendance_card_visitor");
        Assert.Empty(await HistoryAsync(hr, card.Id));
        await AssertCodeAsync(await hr.GetAsync(Assignments), HttpStatusCode.BadRequest, "attendance_filter_invalid");
    }

    [Fact]
    public async Task DeletingAPeriodMakesItsPunchesUnattributedAgain()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, card) = (session.Factory, session.Hr, session.Bob, session.Card);
        var period = await AssignOkAsync(hr, card.Id, bob, At(10, 1));
        await AddPunchesAsync(factory, ("A001", At(10, 2)));
        Assert.Empty((await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!);

        Assert.Equal(HttpStatusCode.NoContent, (await hr.DeleteAsync($"{Assignments}/{period.Id}")).StatusCode);
        Assert.Empty(await HistoryAsync(hr, card.Id));
        Assert.Equal(["A001"], (await hr.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!.Select(x => x.CardNumber));
        await AssertCodeAsync(await hr.DeleteAsync($"{Assignments}/{period.Id}"), HttpStatusCode.NotFound, "attendance_assignment_not_found");
    }

    [Fact]
    public async Task AssignmentChangesAreAuditedIncludingRefusedAttempts()
    {
        await using var session = await HrSession.CreateAsync();
        var (factory, hr, bob, carol, card) = (session.Factory, session.Hr, session.Bob, session.Carol, session.Card);
        var first = await AssignOkAsync(hr, card.Id, bob, At(10, 1));
        await AssertCodeAsync(await AssignAsync(hr, card.Id, carol, At(10, 5)), HttpStatusCode.Conflict, "attendance_card_held");
        var second = await AssignOkAsync(hr, card.Id, carol, At(10, 5), takeOver: true);
        (await hr.PutAsJsonAsync($"{Assignments}/{second.Id}", new CardAssignmentUpdateRequest(carol, null, At(10, 5), At(10, 20), "調職"))).EnsureSuccessStatusCode();
        (await hr.DeleteAsync($"{Assignments}/{first.Id}")).EnsureSuccessStatusCode();

        var changes = await AuditAsync(factory, "admin.attendance_assignment");
        Assert.Equal(["saved", "attendance_card_held", "saved", "saved"], changes.Select(x => x.Result));
        // 接手的快照是該卡的全部期間：後的內容同時看得到被結束的前一段與新段。
        Assert.Contains("A001", changes[2].Details);
        Assert.Contains(first.Id.ToString(), changes[2].Details);
        Assert.Contains(second.Id.ToString(), changes[2].Details);
        Assert.Equal(["saved"], (await AuditAsync(factory, "admin.attendance_assignment_delete")).Select(x => x.Result));
    }

    private static async Task<IReadOnlyList<Guid?>> HolderIdsAsync(AppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<AttributedPunch>().AsNoTracking().OrderBy(x => x.PunchedAt).Select(x => x.UserId).ToListAsync();
    }
}
