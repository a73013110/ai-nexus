using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Office.Attendance.Leave;
using AiNexus.Features.Office.Attendance.Records;
using Microsoft.EntityFrameworkCore;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ListAttendanceRecordsTests
{
    [Fact]
    public async Task LateArrivalEarlyLeaveAndMissingPunchesAreJudgedPerDay()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        await session.PunchAsync("A1", At(19, 9, 5), At(19, 18, 0), At(20, 8, 30), At(20, 17, 45), At(21, 8, 20));
        using var client = await session.Factory.SignedInAsync("bob");

        var person = Assert.Single((await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People);

        var late = Day(person, 19);
        Assert.Equal((AttendanceStatus.Present, 300, 0, At(19, 18, 0)), (late.Status, late.LateSeconds, late.EarlyLeaveSeconds, late.ExpectedClockOut));
        Assert.Equal((At(19, 9, 5), At(19, 18, 0)), (late.ClockIn, late.ClockOut));
        var early = Day(person, 20);
        Assert.Equal((AttendanceStatus.Present, 0, 900), (early.Status, early.LateSeconds, early.EarlyLeaveSeconds));
        var single = Day(person, 21);
        Assert.Equal((AttendanceStatus.Anomaly, AttendanceReason.SinglePunch), (single.Status, single.Reason));
        Assert.Equal((LeaveState.None, 0), (single.Leave, single.Sheets.Count));
    }

    [Fact]
    public async Task TodayIsNotAnAnomalyAndFutureDaysAreNotReturned()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        await session.PunchAsync("A1", At(22, 8, 40));
        using var client = await session.Factory.SignedInAsync("bob");

        var records = await GetAsync(client);
        var person = Assert.Single(records.People);

        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 22)), (records.From, records.To, records.Today));
        Assert.Equal([19, 20, 21, 22], person.Days.Select(x => x.Date.Day));
        Assert.Equal((AttendanceStatus.Anomaly, AttendanceReason.NoPunchOrLeave), (Day(person, 21).Status, Day(person, 21).Reason));
        var today = Day(person, 22);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayInProgress, At(22, 8, 40)), (today.Status, today.Reason, today.ClockIn));

        // 整段都在未來：沒有可回傳的日子，也不查 GD。
        Assert.Empty((await GetAsync(client, "from=2026-10-23&to=2026-10-30")).People);
    }

    [Fact]
    public async Task SheetsAreListedOnEveryDayTheyOverlapWithoutOffsettingLateness()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        session.Gd.AddKind("A", "特休");
        // 上午請假、下午才到：照樣算遲到（從彈性終點 09:00 起算），假單並列給 HR 判斷。
        session.Gd.AddLeave("L-1", GdId("bob"), "A", new(2026, 10, 19, 8, 30, 0), new(2026, 10, 19, 12, 0, 0), "Finish");
        await session.PunchAsync("A1", At(19, 13, 30), At(19, 18, 0));
        session.Gd.AddTravel("T-1", GdId("bob"), new(2026, 10, 20, 9, 0, 0), new(2026, 10, 21, 18, 0, 0), "拜訪南部客戶", "Decide");
        using var client = await session.Factory.SignedInAsync("bob");

        var person = Assert.Single((await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People);

        var halfDay = Day(person, 19);
        Assert.Equal((AttendanceStatus.Present, 4 * 3600 + 1800, LeaveState.Filed), (halfDay.Status, halfDay.LateSeconds, halfDay.Leave));
        var sheet = Assert.Single(halfDay.Sheets);
        Assert.Equal(("L-1", LeaveSheetKind.Leave, "特休", At(19, 8, 30), At(19, 12, 0)), (sheet.DesNo, sheet.Kind, sheet.Title, sheet.StartsAt, sheet.EndsAt));
        // 跨兩天的出差單兩天都列出，沒刷卡的日子是出差而不是異常。
        foreach (var day in new[] { 20, 21 })
        {
            var travel = Day(person, day);
            Assert.Equal((AttendanceStatus.OnLeave, LeaveState.Filed), (travel.Status, travel.Leave));
            Assert.Equal(("T-1", LeaveSheetKind.Travel, "拜訪南部客戶"), (travel.Sheets[0].DesNo, travel.Sheets[0].Kind, travel.Sheets[0].Title));
        }
    }

    [Fact]
    public async Task AnUnreachableGdMarksLeaveUnavailableAndJudgesNoDayAsAnAnomaly()
    {
        await using var session = Create(gdReachable: false);
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        await session.PunchAsync("A1", At(19, 9, 30), At(19, 18, 0));
        using var client = await session.Factory.SignedInAsync("bob");

        var records = await GetAsync(client, "from=2026-10-17&to=2026-10-21&includeDaysOff=true");
        var person = Assert.Single(records.People);

        Assert.Equal((false, false, false), (records.CalendarAvailable, records.LeaveAvailable, person.LeaveAvailable));
        Assert.All(person.Days, x => Assert.Equal(LeaveState.Unavailable, x.Leave));
        Assert.DoesNotContain(person.Days, x => x.Status == AttendanceStatus.Anomaly);
        // 行事曆讀不到：週末照常當放假，平日不知道日型、不判定，有刷卡也不算遲到。
        Assert.Equal([AttendanceStatus.DayOff, AttendanceStatus.DayOff], person.Days.Where(x => x.Date.Day is 17 or 18).Select(x => x.Status));
        var worked = Day(person, 19);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayTypeUnknown, 0, At(19, 9, 30)), (worked.Status, worked.Reason, worked.LateSeconds, worked.ClockIn));
    }

    [Fact]
    public async Task APersonWithoutAGdAccountHasLeaveUnavailableWhileOthersAreJudged()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol", mapped: false);
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        await session.GrantAsync(hr, DataScopes.All);
        using var client = await session.Factory.SignedInAsync("hr");

        var records = await GetAsync(client, "from=2026-10-21&to=2026-10-21");

        Assert.True(records.LeaveAvailable);
        var mapped = Assert.Single(records.People, x => x.UserId == bob);
        Assert.True(mapped.LeaveAvailable);
        Assert.Equal((AttendanceStatus.Anomaly, LeaveState.None), (Day(mapped, 21).Status, Day(mapped, 21).Leave));
        var unmapped = Assert.Single(records.People, x => x.UserId == carol);
        Assert.False(unmapped.LeaveAvailable);
        var day = Day(unmapped, 21);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.LeaveUnavailable, LeaveState.Unavailable), (day.Status, day.Reason, day.Leave));
    }

    [Fact]
    public async Task TheRosterIsCardHoldersOnTheDaysTheyHoldACard()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        var dave = await session.PersonAsync("dave");
        await session.GrantAsync(hr, DataScopes.All);
        await session.HoldAsync(bob, "A1", startsAt: At(20, 10));
        await session.HoldAsync(dave, "A2", endsAt: At(20, 0));
        await session.HoldAsync(carol, "V1");
        await session.HideAsync("V1");
        await session.HoldAsync(null, "A3");
        using var client = await session.Factory.SignedInAsync("hr");

        var people = (await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People;

        // 到職當天從持有那一刻起算整天都列出；離職（持有結束）後的日子不列，不判成沒刷卡。隱藏的卡（例如訪客卡）不在名單。
        Assert.Equal(new[] { bob, dave }.Order(), await PeopleAsync(client, "from=2026-10-19&to=2026-10-21"));
        Assert.Equal([20, 21], Assert.Single(people, x => x.UserId == bob).Days.Select(x => x.Date.Day));
        Assert.Equal([19], Assert.Single(people, x => x.UserId == dave).Days.Select(x => x.Date.Day));
        // 只有姓名的持有人以姓名列出；沒有帳號就對不到公文帳號，假單無法取得。
        var unbound = Assert.Single(people, x => x.UserId is null);
        Assert.Equal(("新進同仁", null, false), (unbound.DisplayName, unbound.Account, unbound.LeaveAvailable));
        Assert.Equal(3, people.Count);
    }

    [Fact]
    public async Task HoldersWithoutAnAccountHangOnTheirCardAndAreVisibleOnlyCompanyWide()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        await session.GrantAsync(hr, DataScopes.All);
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(null, "A2", endsAt: At(20, 0));
        await session.HoldAsync(null, "A3", startsAt: At(20, 0));
        await session.PunchAsync("A2", At(19, 8, 40), At(19, 18, 30));
        await session.PunchAsync("A3", At(21, 8, 50), At(21, 19, 0));
        using var hrClient = await session.Factory.SignedInAsync("hr");
        using var bobClient = await session.Factory.SignedInAsync("bob");

        // 沒有帳號的資料依附在卡號：同名的兩段持有在不同卡上是兩列，同名同姓不會混在一起。
        var all = await GetAsync(hrClient);
        Assert.Equal([("新進同仁", "A2"), ("新進同仁", "A3")], all.People.Where(x => x.UserId is null).Select(x => (x.DisplayName, x.CardNumber!)));
        var person = Assert.Single((await GetAsync(hrClient, Week + "&card=%20A2%20")).People);
        Assert.Equal((null, "A2", "新進同仁", false), (person.UserId, person.CardNumber, person.DisplayName, person.Hidden));
        Assert.Equal((At(19, 8, 40), At(19, 18, 30)), (Day(person, 19).ClockIn, Day(person, 19).ClockOut));
        Assert.Equal([new DateOnly(2026, 10, 19)], person.Days.Select(x => x.Date));
        // 沒有假單可比對，沒刷卡的日子不判異常。
        var later = Assert.Single((await GetAsync(hrClient, Week + "&card=A3")).People);
        Assert.Equal((At(21, 8, 50), At(21, 19, 0)), (Day(later, 21).ClockIn, Day(later, 21).ClockOut));
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.LeaveUnavailable), (Day(later, 20).Status, Day(later, 20).Reason));

        // 卡號只看沒有帳號的資料：指定 bob 的卡不列出他；不是全公司範圍時判斷不了屬於誰，也不在名單。
        Assert.Empty((await GetAsync(hrClient, Week + "&card=A1")).People);
        Assert.DoesNotContain((await GetAsync(bobClient)).People, x => x.UserId is null);
        Assert.Empty((await GetAsync(bobClient, Week + "&card=A2")).People);

        // 隱藏卡號：只略過那張卡，同名的另一張卡照常列出；指定卡號時照常出現並標為隱藏。
        await session.HideAsync("A2");
        Assert.Equal(["A3"], (await GetAsync(hrClient)).People.Where(x => x.UserId is null).Select(x => x.CardNumber!));
        Assert.True(Assert.Single((await GetAsync(hrClient, Week + "&card=A2")).People).Hidden);

        // 人員選單與名單同一套範圍，不限期間；沒有帳號的資料以卡號為選項。
        var options = await OptionsAsync(hrClient);
        Assert.Contains(options, x => x is { UserId: null, CardNumber: "A2", DisplayName: "新進同仁", Holding: false, Hidden: true });
        Assert.Contains(options, x => x is { UserId: null, CardNumber: "A3", Holding: true });
        Assert.Contains(options, x => x.UserId == bob && x.Account is not null && x.Holding);
        Assert.Equal(new Guid?[] { bob }, (await OptionsAsync(bobClient)).Select(x => x.UserId));
    }

    [Fact]
    public async Task PunchesNoPeriodCoversAreNotOnTheRoster()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        await session.GrantAsync(hr, DataScopes.All);
        await session.HoldAsync(bob, "A1");
        // A2 從 20 日起才有姓名持有人：19 日的刷卡沒有持有人。Z9 從沒指派。
        await session.HoldAsync(null, "A2", startsAt: At(20, 0));
        await session.PunchAsync("A2", At(19, 9, 0), At(19, 18, 0), At(20, 8, 50));
        await session.PunchAsync("Z9", At(19, 9, 10), At(21, 12, 0));
        using var hrClient = await session.Factory.SignedInAsync("hr");

        // 沒有持有人的刷卡不屬於任何人，在卡號對應頁以「待指派」處理，不在出勤名單。
        var listed = await GetAsync(hrClient);
        Assert.Equal([(bob, null), (null, "A2")], listed.People.Select(x => (x.UserId, x.CardNumber)));
        Assert.Equal([20, 21, 22], listed.People[1].Days.Select(x => x.Date.Day));
        Assert.Empty((await GetAsync(hrClient, Week + "&card=Z9")).People);
        Assert.DoesNotContain(await OptionsAsync(hrClient), x => x.CardNumber == "Z9");
    }

    [Fact]
    public async Task TheRosterIsPagedByPersonInNameOrder()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        await session.GrantAsync(hr, DataScopes.All);
        foreach (var (account, card) in new[] { ("bob", "A1"), ("carol", "A2"), ("dave", "A3") })
            await session.HoldAsync(await session.PersonAsync(account), card);
        await session.HoldAsync(null, "A4");
        using var client = await session.Factory.SignedInAsync("hr");

        var all = await GetAsync(client);
        var first = await GetAsync(client, Week + "&limit=2");
        var second = await GetAsync(client, Week + "&offset=2&limit=2");
        var beyond = await GetAsync(client, Week + "&offset=4&limit=2");

        Assert.Equal((4, 4, 4, 4), (all.Total, first.Total, second.Total, beyond.Total));
        Assert.Equal((0, 2, 4), (first.Offset, second.Offset, beyond.Offset));
        Assert.Equal(all.People.Select(x => x.DisplayName), first.People.Concat(second.People).Select(x => x.DisplayName));
        Assert.Empty(beyond.People);
        Assert.All(second.People, x => Assert.Equal(4, x.Days.Count));
    }

    [Fact]
    public async Task DaysOffWithoutPunchesOrSheetsAreHiddenUnlessAskedFor()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        session.Gd.AddDay(new(2026, 10, 16), morningOff: true, afternoonOff: true, remark: "補假");
        // 週日加班有刷卡：放假日照樣列出。
        await session.PunchAsync("A1", At(18, 10, 0), At(18, 15, 0));
        using var client = await session.Factory.SignedInAsync("bob");

        var hidden = Assert.Single((await GetAsync(client, "from=2026-10-16&to=2026-10-19")).People);
        Assert.Equal([18, 19], hidden.Days.Select(x => x.Date.Day));
        Assert.Equal(AttendanceDayType.DayOff, Day(hidden, 18).DayType);

        var shown = Assert.Single((await GetAsync(client, "from=2026-10-16&to=2026-10-19&includeDaysOff=true")).People);
        Assert.Equal([16, 17, 18, 19], shown.Days.Select(x => x.Date.Day));
        Assert.Equal(("補假", AttendanceStatus.DayOff), (Day(shown, 16).CalendarRemark, Day(shown, 16).Status));
        Assert.Null(Day(shown, 19).CalendarRemark);
    }

    [Fact]
    public async Task InvalidPagingOrCardNumbersAreRefused()
    {
        await using var session = Create();
        using var client = await session.Factory.SignedInAsync("bob");

        foreach (var query in new[] { "&limit=0", "&limit=51", "&offset=-1", "&card=" + new string('9', 33), $"&card=A1&userId={Guid.NewGuid()}" })
            await AssertCodeAsync(await client.GetAsync($"{Records}?{Week}{query}"), HttpStatusCode.BadRequest, "attendance_records_filter_invalid");
    }

    private static async Task<AttendancePersonOptionDto[]> OptionsAsync(HttpClient client)
        => (await client.GetFromJsonAsync<AttendancePersonOptionDto[]>(Records + "/people"))!;

    [Fact]
    public async Task HiddenCardsAreSkippedOnlyInTheCompanyWideView()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        await session.HideAsync("A2");
        await session.GrantAsync(hr, DataScopes.All);
        using var hrClient = await session.Factory.SignedInAsync("hr");
        using var carolClient = await session.Factory.SignedInAsync("carol");

        Assert.Equal([bob], await PeopleAsync(hrClient));
        // 指定人時照常列出並標為隱藏，人員選單也找得到。
        var named = Assert.Single((await GetAsync(hrClient, Week + $"&userId={carol}")).People);
        Assert.Equal((carol, true, 4), (named.UserId!.Value, named.Hidden, named.Days.Count));
        Assert.Contains(await OptionsAsync(hrClient), x => x.UserId == carol && x.Hidden);
        // 不是隱私控制：本人照樣看得到自己。
        Assert.Equal([carol], await PeopleAsync(carolClient));
    }

    [Fact]
    public async Task AMissingReversedOrOverlongRangeIsRefused()
    {
        await using var session = Create();
        using var client = await session.Factory.SignedInAsync("bob");

        foreach (var query in new[] { "from=2026-10-01", "to=2026-10-31", "from=2026-10-31&to=2026-10-01", "from=2025-10-01&to=2026-10-02", "from=1700-01-01&to=1700-01-02" })
            await AssertCodeAsync(await client.GetAsync($"{Records}?{query}"), HttpStatusCode.BadRequest, "attendance_records_range_invalid");
        // 最長 366 天：閏年也能查整年。
        Assert.Equal(new DateOnly(2026, 10, 1), (await GetAsync(client, "from=2025-10-01&to=2026-10-01")).To);
    }

    [Fact]
    public async Task UsersWithoutTheRecordsPageAreRefused()
    {
        await using var session = Create();
        using var client = await session.Factory.SignedInAsync("bob");
        await session.WithDbAsync(db => db.Set<RoleGroupFeature>()
            .Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords).ExecuteDeleteAsync());

        await AssertCodeAsync(await client.GetAsync($"{Records}?{Week}"), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
