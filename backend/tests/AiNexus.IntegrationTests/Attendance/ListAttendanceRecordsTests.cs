using System.Net;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Attendance;
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

        var records = await GetAsync(client, "from=2026-10-17&to=2026-10-21");
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
    public async Task TheRosterIsEmployeeCardHoldersOnTheDaysTheyHoldACard()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        var dave = await session.PersonAsync("dave");
        await session.GrantAsync(hr, DataScopes.All);
        await session.HoldAsync(bob, "A1", startsAt: At(20, 10));
        await session.HoldAsync(dave, "A2", endsAt: At(20, 0));
        await session.HoldAsync(carol, "V1", kind: CardKinds.Visitor);
        await session.HoldAsync(null, "A3");
        using var client = await session.Factory.SignedInAsync("hr");

        var people = (await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People;

        // 到職當天從持有那一刻起算整天都列出；離職（持有結束）後的日子不列，不判成沒刷卡。訪客卡與只有姓名的持有人不在名單。
        Assert.Equal(new[] { bob, dave }.Order(), people.Select(x => x.UserId).Order());
        Assert.Equal([20, 21], Assert.Single(people, x => x.UserId == bob).Days.Select(x => x.Date.Day));
        Assert.Equal([19], Assert.Single(people, x => x.UserId == dave).Days.Select(x => x.Date.Day));
    }

    [Fact]
    public async Task ExcludedPeopleAreHiddenOnlyFromTheCompanyWideView()
    {
        await using var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        await session.ExcludeAsync(carol);
        await session.GrantAsync(hr, DataScopes.All);
        using var hrClient = await session.Factory.SignedInAsync("hr");
        using var carolClient = await session.Factory.SignedInAsync("carol");

        Assert.Equal([bob], await PeopleAsync(hrClient));
        var all = await GetAsync(hrClient, Week + "&includeExcluded=true");
        Assert.Equal(new[] { bob, carol }.Order(), all.People.Select(x => x.UserId).Order());
        Assert.True(Assert.Single(all.People, x => x.UserId == carol).Excluded);
        Assert.Equal([carol], await PeopleAsync(hrClient, Week + $"&userId={carol}"));
        // 不是隱私控制：本人照樣看得到自己。
        Assert.Equal([carol], await PeopleAsync(carolClient));
    }

    [Fact]
    public async Task AMissingReversedOrOverlongRangeIsRefused()
    {
        await using var session = Create();
        using var client = await session.Factory.SignedInAsync("bob");

        foreach (var query in new[] { "from=2026-10-01", "to=2026-10-31", "from=2026-10-31&to=2026-10-01", "from=2026-10-01&to=2026-11-01", "from=1700-01-01&to=1700-01-02" })
            await AssertCodeAsync(await client.GetAsync($"{Records}?{query}"), HttpStatusCode.BadRequest, "attendance_records_range_invalid");
        Assert.Equal(new DateOnly(2026, 10, 31), (await GetAsync(client, "from=2026-10-01&to=2026-10-31")).To);
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
