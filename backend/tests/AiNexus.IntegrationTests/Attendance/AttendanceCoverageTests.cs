using System.Net;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Gd;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>表單補足遲到、早退與缺勤：查詢與依補足狀態篩選（規則本身見 <c>AttendanceEvaluatorTests</c>）。</summary>
public sealed class AttendanceCoverageTests
{
    /// <summary>
    /// bob：19 日 09:03:06 到、18:10 走，補了 08:30～09:00 核准的假（已補足）；20 日 09:40 到、18:00 走，差 70 分，08:30～10:00 的假還在簽核；21 日沒刷卡也沒請假。
    /// carol：19～21 日都準時。hr 看全公司。
    /// </summary>
    private static async Task<(RecordsSession Session, Guid Bob, Guid Carol, HttpClient Client)> ArrangeAsync()
    {
        var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        await session.GrantAsync(hr, DataScopes.All);
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        session.Gd.AddKind("A", "特休");
        session.Gd.AddUser("G-boss", "boss", name: "林主任");
        session.Gd.AddLeave("L-1", GdId("bob"), "A", new(2026, 10, 19, 8, 30, 0), new(2026, 10, 19, 9, 0, 0), "Decide");
        session.Gd.AddLeave("L-2", GdId("bob"), "A", new(2026, 10, 20, 8, 30, 0), new(2026, 10, 20, 10, 0, 0), "Send");
        session.Gd.AddFlowStep("L-2", "002", "3_G-boss", "核准關卡");
        await session.PunchAsync("A1", At(19, 9, 3, 6), At(19, 18, 10), At(20, 9, 40), At(20, 18, 0));
        await session.PunchAsync("A2", At(19, 8, 30), At(19, 18, 0), At(20, 8, 30), At(20, 18, 0), At(21, 8, 30), At(21, 18, 0));
        return (session, bob, carol, await session.Factory.SignedInAsync("hr"));
    }

    [Fact]
    public async Task EachGapCarriesWhetherFormsCoverIt()
    {
        var (session, bob, _, client) = await ArrangeAsync();
        await using var _session = session;
        using var _client = client;

        var person = Assert.Single((await GetAsync(client, "from=2026-10-19&to=2026-10-21")).People, x => x.UserId == bob);

        var covered = Day(person, 19);
        Assert.Equal((186, GapCoverage.Covered, 0, 1800), (covered.LateSeconds, covered.Coverage, covered.ShortSeconds, covered.FormSeconds));
        Assert.Equal(At(19, 18, 3, 6), covered.AdjustedClockOut);
        var pending = Day(person, 20);
        Assert.Equal((2400, GapCoverage.Pending, 4200, 0), (pending.LateSeconds, pending.Coverage, pending.ShortSeconds, pending.FormSeconds));
        var form = Assert.Single(pending.Forms);
        Assert.Equal((GdFormFlowOver.Send, false, "林主任", "核准關卡"), (form.FlowStatus, form.Approved, form.CurrentSigner, form.CurrentStep));
        var absent = Day(person, 21);
        Assert.Equal((AttendanceStatus.Anomaly, GapCoverage.Unexplained), (absent.Status, absent.Coverage));
    }

    [Fact]
    public async Task TheCoverageFilterListsOnlyMatchingDaysAndThePeopleWhoHaveThem()
    {
        var (session, bob, _, client) = await ArrangeAsync();
        await using var _session = session;
        using var _client = client;

        var unexplained = await GetAsync(client, "from=2026-10-19&to=2026-10-21&coverage=Unexplained");
        var person = Assert.Single(unexplained.People);
        Assert.Equal((bob, 1), (person.UserId!.Value, unexplained.Total));
        Assert.Equal([21], person.Days.Select(x => x.Date.Day));

        var pending = Assert.Single((await GetAsync(client, "from=2026-10-19&to=2026-10-21&coverage=Pending")).People);
        Assert.Equal([20], pending.Days.Select(x => x.Date.Day));
        // 分頁照篩選後的人數。
        Assert.Empty((await GetAsync(client, "from=2026-10-19&to=2026-10-21&coverage=Unexplained&offset=1&limit=10")).People);

        foreach (var value in new[] { "Covered", "Unknown", "nope" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Records}?from=2026-10-19&to=2026-10-21&coverage={value}")).StatusCode);
        await AssertCodeAsync(await client.GetAsync($"{Records}?from=2026-10-19&to=2026-10-21&coverage=Covered"), HttpStatusCode.BadRequest, "attendance_records_filter_invalid");
    }
}
