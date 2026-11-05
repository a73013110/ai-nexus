using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Platform.Time;

namespace AiNexus.UnitTests.Attendance;

/// <summary>判定說明：步驟與結論的數字都來自判定本身（規則見 <c>AttendanceEvaluatorTests</c>），這裡檢查說法與引用的條文。</summary>
public sealed class AttendanceExplanationTests
{
    /// <summary>週三，平日；今天是隔天。</summary>
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static readonly DateOnly Today = Day.AddDays(1);

    private static readonly WorkdayOptions Rules = new();

    private static DateTimeOffset At(int hour, int minute, int second = 0) => TaipeiTime.At(Day, new TimeOnly(hour, minute, second));

    private static AttendanceExplanationDto Explain(DateTimeOffset? first, DateTimeOffset? last, IReadOnlyList<FormPeriod>? forms = null,
        AttendanceDayType type = AttendanceDayType.FullDay, WorkdayOptions? rules = null, DateOnly? today = null)
        => AttendanceExplanation.Explain(new(Day, type, first, last, forms ?? []), rules ?? Rules, today ?? Today);

    private static AttendanceExplanationStep Step(AttendanceExplanationDto explanation, string text)
        => Assert.Single(explanation.Steps, x => x.Text.Contains(text, StringComparison.Ordinal));

    private static void AssertCitesRealRules(AttendanceExplanationDto explanation)
    {
        var titles = AttendanceRules.Describe(Rules).Rules.Select(x => x.Title).ToHashSet();
        Assert.All(explanation.Rules, title => Assert.Contains(title, titles));
    }

    [Fact]
    public void AnOrdinaryDaySaysWhyItIsNormal()
    {
        var explanation = Explain(At(8, 30), At(18, 0));

        Assert.Equal((AttendanceStatus.Present, (GapCoverage?)null), (explanation.Status, explanation.Coverage));
        Assert.Equal(new TimeOnly(8, 30), Step(explanation, "在彈性時段 08:00–09:00 內").At);
        Assert.Equal(new TimeOnly(18, 0), Step(explanation, "從 08:30 起加 9.5 小時").At);
        Assert.Equal(AttendanceExplanationStepKind.Punch, Step(explanation, "不早於應下班").Kind);
        Assert.Equal("正常：沒有遲到，也沒有早退。", explanation.Conclusion);
        Assert.Equal((new TimeOnly(12, 0), new TimeOnly(13, 30)), (explanation.Schedule!.BreakStart, explanation.Schedule.BreakEnd));
        AssertCitesRealRules(explanation);
    }

    [Fact]
    public void AnEarlyLeaveWithoutFormsWalksThroughTheShortfall()
    {
        // 07:30:52 到以 08:00 起算，應下班 17:30；12:49:46 走，扣掉午休只上了 4 小時，差 4 小時。
        var explanation = Explain(At(7, 30, 52), At(12, 49, 46));

        Assert.Equal(AttendanceExplanationStepKind.Punch, Step(explanation, "早於彈性起點 08:00，以 08:00 起算").Kind);
        Assert.Equal(new TimeOnly(17, 30), Step(explanation, "從 08:00 起加 9.5 小時").At);
        var early = Step(explanation, "比應下班早 4 小時 40 分");
        Assert.Equal((AttendanceExplanationStepKind.Gap, new TimeOnly(12, 49, 46)), (early.Kind, early.At));
        Step(explanation, "實際上班 4 小時（扣午休），當天工時 8 小時，差 4 小時。");
        Step(explanation, "當天沒有假單或出差單。");
        Assert.Equal("早退：核准的表單還差 4 小時，未補足。", explanation.Conclusion);
        Assert.Equal((GapCoverage.Unexplained, 4 * 3600), (explanation.Coverage, explanation.ShortSeconds));
        Assert.Equal([AttendanceRules.Titles.Flex, AttendanceRules.Titles.EarlyLeave, AttendanceRules.Titles.Coverage, AttendanceRules.Titles.Forms], explanation.Rules);
    }

    [Fact]
    public void APendingFormIsNamedButDoesNotCoverTheGap()
    {
        // 09:40 到、18:00 走：遲到 40 分，扣午休上了 6 小時 50 分，差 1 小時 10 分；08:30～10:00 的假還在簽核。
        var explanation = Explain(At(9, 40), At(18, 0), [new(At(8, 30), At(10, 0), Approved: false)]);

        Assert.Equal(AttendanceExplanationStepKind.Gap, Step(explanation, "晚於彈性終點 09:00，遲到 40 分").Kind);
        Assert.Equal(new TimeOnly(18, 0), Step(explanation, "遲到，應下班改為表定下班").At);
        Step(explanation, "實際上班 6 小時 50 分（扣午休），當天工時 8 小時，差 1 小時 10 分。");
        Step(explanation, "流程中的表單 1 小時 30 分，核准後才算。");
        Assert.DoesNotContain(explanation.Steps, x => x.Text.StartsWith("核准的表單抵掉", StringComparison.Ordinal));
        Assert.Equal("遲到：核准的表單還差 1 小時 10 分，流程中的單核准後就補足。", explanation.Conclusion);
        AssertCitesRealRules(explanation);
    }

    [Fact]
    public void StayingLateDoesNotCoverBeingLate()
    {
        // 09:10 到、19:00 走：工時夠了，但遲到的 10 分還是要表單；請了 08:30～09:00 就補足，扣表單後 17:40 可下班。
        var explanation = Explain(At(9, 10), At(19, 0), [new(At(8, 30), At(9, 0), Approved: true)]);

        Step(explanation, "遲到的部分（10 分）至少要有表單抵掉，晚走不抵遲到");
        Step(explanation, "核准的表單抵掉 30 分。");
        Assert.Equal(AttendanceExplanationStepKind.Form, Step(explanation, "扣掉核准的表單後").Kind);
        Assert.Equal("遲到：核准的表單已補足。", explanation.Conclusion);
    }

    [Fact]
    public void ADayWithoutPunchesNeedsFormsForTheWholeDay()
    {
        var absent = Explain(null, null);

        Assert.Equal(AttendanceExplanationStepKind.Gap, Step(absent, "整天沒有刷卡").Kind);
        Step(absent, "沒有上班時數，當天工時 8 小時都要有表單。");
        Assert.Equal("未刷卡：核准的表單還差 8 小時，未補足。", absent.Conclusion);

        var leave = Explain(null, null, [new(At(8, 0), At(18, 0), Approved: true)]);
        Assert.Equal((AttendanceStatus.OnLeave, "請假：核准的表單抵掉整天。"), (leave.Status, leave.Conclusion));

        var unknown = AttendanceExplanation.Explain(new(Day, AttendanceDayType.FullDay, null, null, null), Rules, Today);
        Assert.Equal((AttendanceStatus.Undetermined, "無法判定，不算異常。"), (unknown.Status, unknown.Conclusion));
        AssertCitesRealRules(unknown);
    }

    [Fact]
    public void ASinglePunchIsAnAnomalyWithoutLatenessOrEarlyLeave()
    {
        var explanation = Explain(At(8, 40), At(8, 40));

        var punch = Step(explanation, "只有這一筆刷卡");
        Assert.Equal((AttendanceExplanationStepKind.Gap, new TimeOnly(8, 40)), (punch.Kind, punch.At));
        Assert.StartsWith("缺一筆刷卡：", explanation.Conclusion, StringComparison.Ordinal);
        Assert.DoesNotContain(explanation.Steps, x => x.Text.Contains("應下班", StringComparison.Ordinal));
    }

    [Fact]
    public void TodayShowsWhenYouCanLeaveButNotTheLatestPunch()
    {
        // 今天中午匯入時最晚一筆只是當時的刷卡：不說下班、不判遲到早退，只說依上班時間幾點可以走。
        var explanation = Explain(At(8, 45), At(11, 50), today: Day);

        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayInProgress), (explanation.Status, explanation.Reason));
        Assert.Equal(new TimeOnly(18, 15), Step(explanation, "依上班時間算出的應下班").At);
        Assert.DoesNotContain(explanation.Steps, x => x.At == new TimeOnly(11, 50));
        Assert.Equal("今日未結算。", explanation.Conclusion);
    }

    [Fact]
    public void DaysOffAndUnknownDaysListPunchesWithoutJudging()
    {
        var dayOff = Explain(At(10, 0), At(15, 0), type: AttendanceDayType.DayOff);
        Assert.Equal((null, "放假。"), (dayOff.Schedule, dayOff.Conclusion));
        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(15, 0)], dayOff.Steps.Where(x => x.At is not null).Select(x => x.At!.Value));

        var unknown = Explain(At(8, 30), At(18, 0), type: AttendanceDayType.Unknown);
        Assert.Equal("無法判定，不算異常。", unknown.Conclusion);
        AssertCitesRealRules(unknown);
    }

    [Fact]
    public void MinutePrecisionIsExplained()
    {
        var explanation = Explain(At(9, 0, 59), At(18, 30), rules: new WorkdayOptions { PunchPrecision = PunchPrecision.Minute });

        Step(explanation, "在彈性時段 08:00–09:00 內");
        Step(explanation, "刷卡的秒數先捨去再比");
        Assert.Equal("正常：沒有遲到，也沒有早退。", explanation.Conclusion);
    }

    [Theory]
    [InlineData(0, "0 秒")]
    [InlineData(45, "45 秒")]
    [InlineData(600, "10 分")]
    [InlineData(1523, "25 分 23 秒")]
    [InlineData(3600, "1 小時")]
    [InlineData(16814, "4 小時 40 分")]
    public void DurationsUseTheTwoLargestUnits(int seconds, string expected) => Assert.Equal(expected, AttendanceRules.Duration(seconds));
}
