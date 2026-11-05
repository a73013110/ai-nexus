using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Office.Attendance.Records;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>出勤紀錄的單日判定說明：沿用出勤紀錄的查詢，範圍與判定同一份（說明的文字見單元測試 <c>AttendanceExplanationTests</c>）。</summary>
public sealed class ExplainAttendanceDayTests
{
    private const string Explanation = Records + "/explanation";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// 同 <c>AttendanceCoverageTests</c>：bob 19 日已補足、20 日流程中（差 70 分，08:30～10:00 的假停在林主任）、21 日沒刷卡也沒請假；carol 都準時。
    /// hr 看全公司，bob 只看自己。
    /// </summary>
    private static async Task<(RecordsSession Session, Guid Bob, Guid Carol)> ArrangeAsync()
    {
        var session = Create();
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        await session.GrantAsync(hr, DataScopes.All);
        await session.GrantAsync(bob, DataScopes.Own);
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        session.Gd.AddKind("A", "特休");
        session.Gd.AddUser("G-boss", "boss", name: "林主任");
        session.Gd.AddLeave("L-1", GdId("bob"), "A", new(2026, 10, 19, 8, 30, 0), new(2026, 10, 19, 9, 0, 0), "Decide");
        session.Gd.AddLeave("L-2", GdId("bob"), "A", new(2026, 10, 20, 8, 30, 0), new(2026, 10, 20, 10, 0, 0), "Send");
        session.Gd.AddFlow("FLOW_CURRENT", "L-2", "002", "3_G-boss", "核准關卡");
        await session.PunchAsync("A1", At(19, 9, 3, 6), At(19, 18, 10), At(20, 9, 40), At(20, 18, 0));
        await session.PunchAsync("A2", At(19, 8, 30), At(19, 18, 0), At(20, 8, 30), At(20, 18, 0), At(21, 8, 30), At(21, 18, 0));
        return (session, bob, carol);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    [Fact]
    public async Task TheExplanationWalksThroughTheSameDayTheRecordsShow()
    {
        var (session, bob, _) = await ArrangeAsync();
        await using var _session = session;
        using var hr = await session.Factory.SignedInAsync("hr");

        var pending = await ReadAsync<AttendanceExplanationDto>(await hr.GetAsync($"{Explanation}?date=2026-10-20&userId={bob}"));

        var record = Day(Assert.Single((await GetAsync(hr)).People, x => x.UserId == bob), 20);
        Assert.Equal((record.Status, record.Coverage, record.ShortSeconds), (pending.Status, pending.Coverage, pending.ShortSeconds));
        Assert.Contains(pending.Steps, x => x.Kind == AttendanceExplanationStepKind.Gap && x.At == new TimeOnly(9, 40));
        Assert.Contains(pending.Steps, x => x.Kind == AttendanceExplanationStepKind.Form && x.Text.Contains("流程中的表單 1 小時 30 分", StringComparison.Ordinal));
        Assert.Equal("遲到：核准的表單還差 1 小時 10 分，流程中的單核准後就補足。", pending.Conclusion);

        var covered = await ReadAsync<AttendanceExplanationDto>(await hr.GetAsync($"{Explanation}?date=2026-10-19&userId={bob}"));
        Assert.Equal((GapCoverage.Covered, "遲到：核准的表單已補足。"), (covered.Coverage, covered.Conclusion));
    }

    [Fact]
    public async Task OnlyPeopleAndDaysTheViewerCanSeeAreExplained()
    {
        var (session, bob, carol) = await ArrangeAsync();
        await using var _session = session;
        using var client = await session.Factory.SignedInAsync("bob");

        Assert.Equal(AttendanceStatus.Anomaly, (await ReadAsync<AttendanceExplanationDto>(await client.GetAsync($"{Explanation}?date=2026-10-21&userId={bob}"))).Status);
        // 範圍外的人、未來的日子與沒持卡的日子都是同一個「找不到」，不透露差別。
        await AssertCodeAsync(await client.GetAsync($"{Explanation}?date=2026-10-21&userId={carol}"), HttpStatusCode.NotFound, "attendance_record_not_found");
        await AssertCodeAsync(await client.GetAsync($"{Explanation}?date=2026-10-23&userId={bob}"), HttpStatusCode.NotFound, "attendance_record_not_found");
        await AssertCodeAsync(await client.GetAsync($"{Explanation}?date=2026-09-30&userId={bob}"), HttpStatusCode.NotFound, "attendance_record_not_found");

        foreach (var query in new[] { $"userId={bob}", "date=2026-10-21", $"date=2026-10-21&userId={bob}&card=A1" })
            await AssertCodeAsync(await client.GetAsync($"{Explanation}?{query}"), HttpStatusCode.BadRequest, "attendance_records_filter_invalid");
    }
}
