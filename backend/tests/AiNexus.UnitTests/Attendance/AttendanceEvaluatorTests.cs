using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Calendar;
using AiNexus.Features.Office.Attendance.Records;

namespace AiNexus.UnitTests.Attendance;

public sealed class AttendanceEvaluatorTests
{
    /// <summary>週三，平日。</summary>
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static readonly WorkdayOptions Rules = new();

    private static DateTimeOffset At(int hour, int minute, int second = 0) => TaipeiTime.At(Day, new TimeOnly(hour, minute, second));

    private static AttendanceEvaluation Evaluate(DateTimeOffset? first, DateTimeOffset? last, AttendanceDayType type = AttendanceDayType.FullDay, params FormPeriod[] forms)
        => AttendanceEvaluator.Evaluate(new(Day, type, first, last, forms), Rules);

    /// <summary>表單無法取得（GD 連不上或對不到公文帳號）。</summary>
    private static AttendanceEvaluation EvaluateWithoutForms(DateTimeOffset? first, DateTimeOffset? last)
        => AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, first, last, null), Rules);

    private static FormPeriod Approved(DateTimeOffset start, DateTimeOffset end) => new(start, end, Approved: true);

    private static FormPeriod Pending(DateTimeOffset start, DateTimeOffset end) => new(start, end, Approved: false);

    private static int Minutes(int minutes) => minutes * 60;

    /// <summary>遲到、早退以秒為單位。</summary>
    private static void AssertPresent(AttendanceEvaluation result, DateTimeOffset expectedClockOut, int lateSeconds, int earlySeconds)
    {
        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.Null(result.Reason);
        Assert.Equal(expectedClockOut, result.ExpectedClockOut);
        Assert.Equal((lateSeconds, earlySeconds), (result.LateSeconds, result.EarlyLeaveSeconds));
    }

    [Fact]
    public void ArrivingAndLeavingOnScheduleIsNeitherLateNorEarly()
    {
        var result = Evaluate(At(8, 30), At(18, 0));

        AssertPresent(result, At(18, 0), 0, 0);
        Assert.Equal((At(8, 30), At(18, 0)), (result.ClockIn, result.ClockOut));
    }

    [Fact]
    public void ArrivingWithinTheFlexWindowShiftsTheExpectedClockOut()
    {
        AssertPresent(Evaluate(At(8, 50), At(18, 20)), At(18, 20), 0, 0);
        AssertPresent(Evaluate(At(9, 0), At(18, 10)), At(18, 30), 0, Minutes(20));
    }

    [Fact]
    public void ArrivingAfterTheFlexWindowIsLateFromTheFlexEnd()
        => AssertPresent(Evaluate(At(9, 5), At(18, 0)), At(18, 0), Minutes(5), 0);

    [Fact]
    public void LeavingBeforeTheExpectedClockOutIsEarly() => AssertPresent(Evaluate(At(8, 30), At(17, 45)), At(18, 0), 0, Minutes(15));

    [Fact]
    public void ArrivingBeforeTheFlexStartCountsFromTheFlexStart()
    {
        var result = Evaluate(At(7, 40), At(17, 30));

        AssertPresent(result, At(17, 30), 0, 0);
        Assert.Equal(At(7, 40), result.ClockIn);
    }

    [Fact]
    public void ALateArrivalIsExpectedToLeaveAtTheScheduledEnd()
    {
        AssertPresent(Evaluate(At(9, 1), At(17, 50)), At(18, 0), Minutes(1), Minutes(10));
        AssertPresent(Evaluate(At(10, 0), At(19, 30)), At(18, 0), Minutes(60), 0);
    }

    [Fact]
    public void AMorningOffDayUsesTheAfternoonRule()
    {
        AssertPresent(Evaluate(At(13, 10), At(17, 40), AttendanceDayType.MorningOff), At(17, 40), 0, 0);
        AssertPresent(Evaluate(At(12, 30), At(17, 20), AttendanceDayType.MorningOff), At(17, 30), 0, Minutes(10));
        AssertPresent(Evaluate(At(14, 5), At(18, 0), AttendanceDayType.MorningOff), At(18, 0), Minutes(5), 0);
    }

    [Fact]
    public void AnAfternoonOffDayUsesTheMorningRule()
    {
        AssertPresent(Evaluate(At(8, 0), At(11, 30), AttendanceDayType.AfternoonOff), At(11, 30), 0, 0);
        AssertPresent(Evaluate(At(7, 50), At(11, 25), AttendanceDayType.AfternoonOff), At(11, 30), 0, Minutes(5));
        AssertPresent(Evaluate(At(9, 30), At(12, 0), AttendanceDayType.AfternoonOff), At(12, 0), Minutes(30), 0);
    }

    /// <summary>表單的部分：補足狀態、還差的秒數、扣表單後的應下班、核准表單的秒數。</summary>
    private static void AssertForms(AttendanceEvaluation result, GapCoverage? coverage, int shortSeconds, DateTimeOffset? adjustedClockOut, int formSeconds)
        => Assert.Equal((coverage, shortSeconds, adjustedClockOut, formSeconds), (result.Coverage, result.ShortSeconds, result.AdjustedClockOut, result.FormSeconds));

    [Fact]
    public void HrExamplesSubtractTheFormHoursFromTheWorkday()
    {
        // 07/03：09:00:20 到、請 16:00～18:00。等於 9:00 到 16:30 走：扣 2 小時後 16:30:20 可下班，16:37:08 走已補足。
        var first = Evaluate(At(9, 0, 20), At(16, 37, 8), forms: Approved(At(16, 0), At(18, 0)));
        AssertPresent(first, At(18, 0), 20, 4972);
        AssertForms(first, GapCoverage.Covered, 0, At(16, 30, 20), Minutes(120));

        // 07/20：09:01:19 到、請 08:30～09:00。等於 08:31:19 到，18:01:19 才可下班；17:30:22 走還差 30 分 57 秒。
        var second = Evaluate(At(9, 1, 19), At(17, 30, 22), forms: Approved(At(8, 30), At(9, 0)));
        AssertPresent(second, At(18, 0), 79, 1778);
        AssertForms(second, GapCoverage.Unexplained, 1857, At(18, 1, 19), Minutes(30));

        // 07/30：08:58:40 到、請 16:30～18:00。8 小時扣 1.5 小時，16:58:40 可下班，17:11:02 走已補足。
        var third = Evaluate(At(8, 58, 40), At(17, 11, 2), forms: Approved(At(16, 30), At(18, 0)));
        AssertPresent(third, At(18, 28, 40), 0, 4658);
        AssertForms(third, GapCoverage.Covered, 0, At(16, 58, 40), Minutes(90));
    }

    [Fact]
    public void LatenessWithoutFormsKeepsTheOldJudgementButNeedsTheMissingHours()
    {
        // 遲到照舊系統：10 分、表定下班。但 09:10～18:00 只上了 7 小時 20 分，表單要補 40 分。
        var leaving = Evaluate(At(9, 10), At(18, 0));
        AssertPresent(leaving, At(18, 0), Minutes(10), 0);
        AssertForms(leaving, GapCoverage.Unexplained, Minutes(40), null, 0);

        Assert.Equal(Minutes(30), Evaluate(At(9, 10), At(18, 10)).ShortSeconds);
        // 晚走不抵遲到：上滿 9 個多小時，遲到的 10 分仍要表單。
        Assert.Equal(Minutes(10), Evaluate(At(9, 10), At(20, 0)).ShortSeconds);
    }

    [Fact]
    public void FormsThatCoverTheLatenessMoveTheClockOut()
    {
        // 請 08:30～09:30：等於 08:10 到，17:40 可下班。
        AssertForms(Evaluate(At(9, 10), At(18, 0), forms: Approved(At(8, 30), At(9, 30))), GapCoverage.Covered, 0, At(17, 40), Minutes(60));
        // 只請 08:30～09:00：等於 08:40 到，18:10 才可下班。
        AssertForms(Evaluate(At(9, 10), At(18, 0), forms: Approved(At(8, 30), At(9, 0))), GapCoverage.Unexplained, Minutes(10), At(18, 10), Minutes(30));
        // 剛好請遲到的 10 分：等於 09:00 到，18:30 才可下班。
        AssertForms(Evaluate(At(9, 10), At(18, 0), forms: Approved(At(9, 0), At(9, 10))), GapCoverage.Unexplained, Minutes(30), At(18, 30), Minutes(10));
        // 表單不夠抵遲到：仍是遲到，沒有補得足的下班時間。
        AssertForms(Evaluate(At(9, 10), At(18, 0), forms: Approved(At(8, 30), At(8, 35))), GapCoverage.Unexplained, Minutes(35), null, Minutes(5));
    }

    [Fact]
    public void FormsCountByHoursWhereverTheyFall()
    {
        // 早退 1.5 小時，請的是上午 08:30～10:00（人在公司期間）：時數夠就補足。
        AssertForms(Evaluate(At(8, 30), At(16, 30), forms: Approved(At(8, 30), At(10, 0))), GapCoverage.Covered, 0, At(16, 30), Minutes(90));
        AssertForms(Evaluate(At(8, 30), At(16, 30), forms: Approved(At(16, 30), At(18, 0))), GapCoverage.Covered, 0, At(16, 30), Minutes(90));
        // 只算彈性起點到最晚應下班（09:00＋9.5 小時＝18:30）之間。
        Assert.Equal(0, Evaluate(At(8, 30), At(16, 30), forms: Approved(At(6, 0), At(8, 0))).FormSeconds);
        Assert.Equal(Minutes(30), Evaluate(At(8, 30), At(16, 30), forms: Approved(At(18, 0), At(20, 0))).FormSeconds);
        // 跨日的表單只算當天。
        Assert.Equal(Minutes(480), Evaluate(At(8, 30), At(10, 0), forms: Approved(At(8, 30).AddDays(-1), At(18, 0).AddDays(1))).FormSeconds);
    }

    [Fact]
    public void FormsWithoutAGapOnlyReportTheAdjustedClockOut()
    {
        var result = Evaluate(At(8, 30), At(18, 0), forms: Approved(At(10, 0), At(11, 0)));

        AssertPresent(result, At(18, 0), 0, 0);
        AssertForms(result, null, 0, At(17, 0), Minutes(60));
    }

    [Fact]
    public void AFormForTheWholeDayCoversAnyPunches()
    {
        // 刷卡後請整天假回家：早退很多，但表單抵掉整天，沒有應下班。
        var result = Evaluate(At(8, 30), At(10, 0), forms: Approved(At(8, 30), At(18, 0)));

        AssertPresent(result, At(18, 0), 0, Minutes(480));
        AssertForms(result, GapCoverage.Covered, 0, null, Minutes(480));
    }

    [Fact]
    public void PendingFormsOnlyMarkTheGapAsPending()
    {
        AssertForms(Evaluate(At(9, 10), At(18, 0), forms: Pending(At(8, 30), At(9, 30))), GapCoverage.Pending, Minutes(40), null, 0);
        // 核准的不夠，加上流程中的才夠：還差的只看核准的。
        AssertForms(Evaluate(At(9, 10), At(18, 0), forms: [Approved(At(8, 30), At(9, 0)), Pending(At(9, 0), At(9, 30))]),
            GapCoverage.Pending, Minutes(10), At(18, 10), Minutes(30));
        Assert.Equal(GapCoverage.Unexplained, Evaluate(At(9, 40), At(18, 0), forms: Pending(At(8, 30), At(9, 0))).Coverage);
    }

    [Fact]
    public void TheLunchBreakIsNeitherWorkNorFormHours()
    {
        // 上午請假、13:25 才到：13:30～18:00 上 4.5 小時，加上 08:30～12:00 的 3.5 小時剛好 8 小時。
        var morning = Evaluate(At(13, 25), At(18, 0), forms: Approved(At(8, 30), At(12, 0)));
        AssertPresent(morning, At(18, 0), Minutes(265), 0);
        AssertForms(morning, GapCoverage.Covered, 0, At(18, 0), Minutes(210));
        // 只請到 11:30：差 30 分，18:30 才可下班。
        AssertForms(Evaluate(At(13, 25), At(18, 0), forms: Approved(At(8, 30), At(11, 30))), GapCoverage.Unexplained, Minutes(30), At(18, 30), Minutes(180));
        // 表單寫 10:00～13:30，午休那段不算，只有 2 小時；14:00～18:00 上 4 小時，差 2 小時。
        AssertForms(Evaluate(At(14, 0), At(18, 0), forms: Approved(At(10, 0), At(13, 30))), GapCoverage.Unexplained, Minutes(120), null, Minutes(120));
        // 12:00 離開、請下午 13:30～18:00：上午 3.5 小時＋4.5 小時，補足；11:00 離開差 1 小時。
        AssertForms(Evaluate(At(8, 30), At(12, 0), forms: Approved(At(13, 30), At(18, 0))), GapCoverage.Covered, 0, At(12, 0), Minutes(270));
        Assert.Equal(Minutes(60), Evaluate(At(8, 30), At(11, 0), forms: Approved(At(13, 30), At(18, 0))).ShortSeconds);
    }

    [Fact]
    public void NoPunchIsOnLeaveOnlyWhenApprovedFormsCoverTheDay()
    {
        var anomaly = Evaluate(null, null);
        Assert.Equal((AttendanceStatus.Anomaly, AttendanceReason.NoPunchOrLeave), (anomaly.Status, anomaly.Reason));
        AssertForms(anomaly, GapCoverage.Unexplained, Minutes(480), null, 0);
        Assert.Null(anomaly.ExpectedClockOut);

        var onLeave = Evaluate(null, null, forms: Approved(At(8, 30), At(18, 0)));
        Assert.Equal((AttendanceStatus.OnLeave, (AttendanceReason?)null, FormState.Filed), (onLeave.Status, onLeave.Reason, onLeave.FormState));
        AssertForms(onLeave, null, 0, null, Minutes(480));
        // 跨日的表單只算當天；上午、下午兩張單合起來也補足。
        Assert.Equal(AttendanceStatus.OnLeave, Evaluate(null, null, forms: Approved(At(8, 30).AddDays(-1), At(18, 0).AddDays(1))).Status);
        Assert.Equal(AttendanceStatus.OnLeave, Evaluate(null, null, forms: [Approved(At(8, 30), At(12, 0)), Approved(At(13, 30), At(18, 0))]).Status);

        AssertForms(Evaluate(null, null, forms: Approved(At(8, 30), At(12, 0))), GapCoverage.Unexplained, Minutes(270), null, Minutes(210));
        AssertForms(Evaluate(null, null, forms: [Approved(At(8, 30), At(12, 0)), Pending(At(13, 30), At(18, 0))]), GapCoverage.Pending, Minutes(270), null, Minutes(210));
        // 重疊的表單不重複算時數。
        Assert.Equal(Minutes(270), Evaluate(null, null, forms: [Approved(At(8, 30), At(12, 0)), Approved(At(9, 0), At(12, 0))]).ShortSeconds);
    }

    [Fact]
    public void HalfDayTypesNeedOnlyTheirOwnHours()
    {
        Assert.Equal(AttendanceStatus.OnLeave, Evaluate(null, null, AttendanceDayType.MorningOff, Approved(At(13, 30), At(18, 0))).Status);
        Assert.Equal(AttendanceStatus.OnLeave, Evaluate(null, null, AttendanceDayType.AfternoonOff, Approved(At(8, 30), At(12, 0))).Status);
        // 上午放假、14:10 到、請 13:30～14:30：等於 13:10 到，17:40 可下班。
        AssertForms(Evaluate(At(14, 10), At(18, 0), AttendanceDayType.MorningOff, Approved(At(13, 30), At(14, 30))), GapCoverage.Covered, 0, At(17, 40), Minutes(60));
    }

    [Fact]
    public void ASinglePunchIsCoveredOnlyByFormsForTheWholeDay()
    {
        var covered = Evaluate(At(8, 25), At(8, 25), forms: Approved(At(8, 30), At(18, 0)));
        Assert.Equal((AttendanceStatus.Anomaly, AttendanceReason.SinglePunch), (covered.Status, covered.Reason));
        AssertForms(covered, GapCoverage.Covered, 0, null, Minutes(480));

        Assert.Equal(Minutes(480), Evaluate(At(8, 25), At(8, 25)).ShortSeconds);
        // 13:30 只刷一次、上午有假：分不出下午有沒有上班，還差下午的時數。
        AssertForms(Evaluate(At(13, 30), null, forms: Approved(At(8, 30), At(12, 0))), GapCoverage.Unexplained, Minutes(270), null, Minutes(210));
    }

    [Fact]
    public void UnavailableFormsAreUndeterminedInsteadOfAnAnomalyAndDoNotBlockPunchJudgement()
    {
        var missing = EvaluateWithoutForms(null, null);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.FormsUnavailable, (GapCoverage?)null), (missing.Status, missing.Reason, missing.Coverage));

        var present = EvaluateWithoutForms(At(9, 10), At(18, 0));
        AssertPresent(present, At(18, 0), Minutes(10), 0);
        Assert.Equal(FormState.Unavailable, present.FormState);
        AssertForms(present, GapCoverage.Unknown, 0, null, 0);
        Assert.Equal(GapCoverage.Unknown, EvaluateWithoutForms(At(9, 10), null).Coverage);
        Assert.Null(EvaluateWithoutForms(At(8, 30), At(18, 0)).Coverage);
    }

    [Fact]
    public void GapsListTheRawLateEarlyAndAbsentPeriodsRegardlessOfForms()
    {
        // 遲到從彈性終點算到上班，早退從下班算到應下班（遲到時是表定下班）；表單不改時段，差多少看 ShortSeconds。
        Assert.Equal(
            [new(AttendanceGapKind.Late, At(9, 0), At(9, 16, 13)), new(AttendanceGapKind.EarlyLeave, At(17, 45), At(18, 0))],
            Evaluate(At(9, 16, 13), At(17, 45), forms: Approved(At(9, 0), At(9, 10))).Gaps);
        Assert.Equal([new(AttendanceGapKind.EarlyLeave, At(17, 0), At(17, 50))], Evaluate(At(8, 20), At(17, 0)).Gaps);
        Assert.Equal([new(AttendanceGapKind.Absent, At(8, 30), At(18, 0))], Evaluate(null, null, forms: Pending(At(8, 30), At(12, 0))).Gaps);
        Assert.Equal([new(AttendanceGapKind.Absent, At(13, 30), At(18, 0))], Evaluate(null, null, AttendanceDayType.MorningOff).Gaps);

        // 沒有缺口、請假、只有一筆刷卡（分不出缺哪段）與今天都沒有時段。
        Assert.Empty(Evaluate(At(8, 30), At(18, 0)).Gaps);
        Assert.Empty(Evaluate(null, null, forms: Approved(At(8, 30), At(18, 0))).Gaps);
        Assert.Empty(Evaluate(At(8, 25), At(8, 25)).Gaps);
        Assert.Empty(AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, At(9, 30), At(18, 0), []), Rules, Day).Gaps);
    }

    /// <summary>
    /// 所有上下班時間（每 7 分 13 秒取一點，涵蓋彈性前、午休中、深夜）配上逐步加長的表單，檢查判定的整體性質，
    /// 免得只驗了幾個例子、別的時間點出錯。
    /// </summary>
    [Theory]
    [InlineData(AttendanceDayType.FullDay)]
    [InlineData(AttendanceDayType.MorningOff)]
    [InlineData(AttendanceDayType.AfternoonOff)]
    public void TheFormRuleIsConsistentForEveryPunchTime(AttendanceDayType type)
    {
        var step = new TimeSpan(0, 7, 13);
        var times = new List<DateTimeOffset>();
        for (var t = At(6, 0); t < At(23, 0); t += step) times.Add(t);
        // 表單從 06:00 起每次加長 20 分，時數只增不減。
        var forms = Enumerable.Range(0, 40).Select(k => k == 0 ? [] : new[] { Approved(At(6, 0), At(6, 0).AddMinutes(20 * k)) }).ToList();

        foreach (var clockIn in times)
        foreach (var clockOut in times.Where(x => x > clockIn))
        {
            var plain = Evaluate(clockIn, clockOut, type);
            // 沒有表單：有缺口就是未補足，與舊系統的遲到早退同時有、同時沒有；不看表單時沒有扣表單後的應下班。
            Assert.Equal(plain.LateSeconds > 0 || plain.EarlyLeaveSeconds > 0, plain.Coverage is not null);
            Assert.True(plain.Coverage is null or GapCoverage.Unexplained);
            Assert.Null(plain.AdjustedClockOut);
            // 缺口時段與遲到、早退秒數一一對應。
            Assert.Equal(plain.LateSeconds > 0, plain.Gaps.Any(x => x.Kind == AttendanceGapKind.Late));
            Assert.Equal(plain.EarlyLeaveSeconds > 0, plain.Gaps.Any(x => x.Kind == AttendanceGapKind.EarlyLeave));

            var results = forms.Select(x => Evaluate(clockIn, clockOut, type, x)).ToList();
            for (var k = 0; k < results.Count; k++)
            {
                var result = results[k];
                // 表單不改原始判定。
                Assert.Equal((plain.ExpectedClockOut, plain.LateSeconds, plain.EarlyLeaveSeconds), (result.ExpectedClockOut, result.LateSeconds, result.EarlyLeaveSeconds));
                Assert.Equal(plain.Coverage is null, result.Coverage is null);
                Assert.True(result.Coverage is null or GapCoverage.Covered or GapCoverage.Unexplained);
                Assert.Equal(result.Coverage == GapCoverage.Unexplained, result.ShortSeconds > 0);
                // 扣表單後的應下班就是補足的界線：之後走就補足。
                if (result is { AdjustedClockOut: { } adjusted, Coverage: not null })
                    Assert.Equal(clockOut >= adjusted, result.Coverage == GapCoverage.Covered);
                // 還差多少就是要再多多少表單：時數達到的那一張起補足，之前都不夠。
                if (result.Coverage == GapCoverage.Unexplained)
                {
                    foreach (var more in results.Skip(k + 1))
                        Assert.Equal(more.FormSeconds >= result.FormSeconds + result.ShortSeconds, more.Coverage == GapCoverage.Covered);
                }
            }
        }
    }

    /// <summary>
    /// 舊系統 EmpClock（彈性間隔 1 分鐘）的全天判定：刷卡捨去秒、早於彈性起點以起點計，晚於彈性終點為遲到並改為表定下班，否則應下班＝上班＋9.5 小時；
    /// 下班捨去秒後早於應下班為早退。由 2026/06～10 舊系統與本系統 1,962 個人日逐日比對歸納，有刷卡兩筆以上的日子全部吻合。
    /// 刷卡剛好在整分時沒有捨去秒的差別，兩套判定必須一字不差，小計才會與舊系統相同；這裡逐分鐘窮舉，免得改規則時悄悄偏離。
    /// </summary>
    [Fact]
    public void WholeMinutePunchesMatchTheLegacyJudgementExactly()
    {
        static (bool Late, bool Early, TimeSpan Expected) Legacy(TimeSpan clockIn, TimeSpan clockOut)
        {
            var late = clockIn > new TimeSpan(9, 0, 0);
            var expected = late ? new TimeSpan(18, 0, 0) : (clockIn < new TimeSpan(8, 0, 0) ? new TimeSpan(8, 0, 0) : clockIn) + TimeSpan.FromHours(9.5);
            return (late, clockOut < expected, expected);
        }

        for (var clockIn = new TimeSpan(6, 0, 0); clockIn < new TimeSpan(23, 0, 0); clockIn += TimeSpan.FromMinutes(1))
        for (var clockOut = clockIn + TimeSpan.FromMinutes(1); clockOut < new TimeSpan(24, 0, 0); clockOut += TimeSpan.FromMinutes(1))
        {
            var result = Evaluate(At(clockIn.Hours, clockIn.Minutes), At(clockOut.Hours, clockOut.Minutes));
            var legacy = Legacy(clockIn, clockOut);
            Assert.Equal((legacy.Late, legacy.Early, TaipeiTime.At(Day, TimeOnly.FromTimeSpan(legacy.Expected))),
                (result.LateSeconds > 0, result.EarlyLeaveSeconds > 0, result.ExpectedClockOut!.Value));
        }
    }

    [Fact]
    public void ADayOffIsNotJudgedButStillReturnsThePunches()
    {
        var worked = Evaluate(At(10, 0), At(12, 0), AttendanceDayType.DayOff);
        Assert.Equal((AttendanceStatus.DayOff, (AttendanceReason?)null), (worked.Status, worked.Reason));
        Assert.Equal((At(10, 0), At(12, 0)), (worked.ClockIn, worked.ClockOut));
        Assert.Equal((null, 0, 0), (worked.ExpectedClockOut, worked.LateSeconds, worked.EarlyLeaveSeconds));

        Assert.Equal(AttendanceStatus.DayOff, Evaluate(null, null, AttendanceDayType.DayOff).Status);
    }

    [Fact]
    public void AnUnknownDayTypeIsUndeterminedAndStillReturnsThePunches()
    {
        var result = Evaluate(At(9, 30), At(18, 0), AttendanceDayType.Unknown);

        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayTypeUnknown), (result.Status, result.Reason));
        Assert.Equal((At(9, 30), At(18, 0)), (result.ClockIn, result.ClockOut));
        Assert.Equal(0, result.LateSeconds);
    }

    [Fact]
    public void ASinglePunchIsAnAnomalyButNeitherLateNorEarly()
    {
        var samePunch = new (DateTimeOffset?, DateTimeOffset?)[]
        {
            (At(8, 20), At(8, 20)), (At(17, 50), null), (null, At(17, 50)), (At(8, 20, 5).AddMilliseconds(100), At(8, 20, 5).AddMilliseconds(900))
        };
        foreach (var (first, last) in samePunch)
        {
            var result = Evaluate(first, last);
            Assert.Equal((AttendanceStatus.Anomaly, AttendanceReason.SinglePunch), (result.Status, result.Reason));
            Assert.Equal(result.ClockIn, result.ClockOut);
            Assert.NotNull(result.ClockIn);
            Assert.Equal((null, 0, 0), (result.ExpectedClockOut, result.LateSeconds, result.EarlyLeaveSeconds));
        }

        // 不同秒就是兩筆刷卡，照常判定。
        Assert.Equal(AttendanceStatus.Present, Evaluate(At(8, 20, 5), At(8, 20, 6)).Status);
    }

    [Fact]
    public void TimesAreComparedToTheSecond()
    {
        AssertPresent(Evaluate(At(9, 0, 0), At(18, 30, 0)), At(18, 30), 0, 0);
        AssertPresent(Evaluate(At(9, 0, 1), At(18, 0, 0)), At(18, 0), 1, 0);
        AssertPresent(Evaluate(At(8, 30, 0), At(17, 59, 59)), At(18, 0), 0, 1);

        var result = Evaluate(At(8, 10, 30), At(17, 40, 29));
        AssertPresent(result, At(17, 40, 30), 0, 1);
        Assert.Equal((At(8, 10, 30), At(17, 40, 29)), (result.ClockIn, result.ClockOut));
    }

    private static readonly WorkdayOptions MinuteRules = new() { PunchPrecision = PunchPrecision.Minute };

    private static AttendanceEvaluation EvaluateByMinute(DateTimeOffset? first, DateTimeOffset? last, params FormPeriod[] forms)
        => AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, first, last, forms), MinuteRules);

    /// <summary>整分（舊系統）：秒數捨去後再比，回傳的刷卡照樣到秒。例子是舊系統與本系統以秒計時判得不同的日子。</summary>
    [Fact]
    public void WholeMinutePrecisionDropsTheSecondsBeforeComparing()
    {
        // 09:00 那一分鐘內到不算遲到，應下班照彈性算 18:30，18:06 走是早退 24 分（以秒計會判成遲到、應下班 18:00、不算早退）。
        var withinTheMinute = EvaluateByMinute(At(9, 0, 32), At(18, 6, 10));
        AssertPresent(withinTheMinute, At(18, 30), 0, Minutes(24));
        Assert.Equal((At(9, 0, 32), At(18, 6, 10)), (withinTheMinute.ClockIn, withinTheMinute.ClockOut));
        Assert.Equal([new(AttendanceGapKind.EarlyLeave, At(18, 6), At(18, 30))], withinTheMinute.Gaps);

        // 早走 19 秒、同一分鐘內：舊系統不算早退。
        AssertPresent(EvaluateByMinute(At(8, 41, 30), At(18, 11, 11)), At(18, 11), 0, 0);
        AssertPresent(Evaluate(At(8, 41, 30), At(18, 11, 11)), At(18, 11, 30), 0, 19);

        AssertPresent(EvaluateByMinute(At(9, 0, 59), At(18, 30)), At(18, 30), 0, 0);
        AssertPresent(EvaluateByMinute(At(9, 1, 0), At(18, 0)), At(18, 0), Minutes(1), 0);
        AssertPresent(EvaluateByMinute(At(9, 5, 40), At(17, 59, 59)), At(18, 0), Minutes(5), Minutes(1));

        // 表單補足也用捨去後的時間：09:01:19 視為 09:01，以秒計的差 1857 秒變成 1860 秒，扣表單後 18:01 可走。
        var withForm = EvaluateByMinute(At(9, 1, 19), At(17, 30, 22), Approved(At(8, 30), At(9, 0)));
        Assert.Equal((GapCoverage.Unexplained, 1860, At(18, 1)), (withForm.Coverage, withForm.ShortSeconds, withForm.AdjustedClockOut));

        // 同一分鐘內的兩筆仍是兩筆刷卡，不是只有一筆。
        Assert.Equal(AttendanceStatus.Present, EvaluateByMinute(At(8, 20, 5), At(8, 20, 40)).Status);
        Assert.Equal(AttendanceReason.SinglePunch, EvaluateByMinute(At(8, 20, 5), At(8, 20, 5)).Reason);
    }

    /// <summary>
    /// 整分時，帶秒數的刷卡與捨去秒數的整分刷卡判定完全相同；整分刷卡已由 <see cref="WholeMinutePunchesMatchTheLegacyJudgementExactly"/> 窮舉對過舊系統，
    /// 兩者合起來等於任何刷卡都與舊系統一致。秒數與時間隨機抽樣（固定種子，失敗可重現）。
    /// </summary>
    [Fact]
    public void WholeMinutePrecisionJudgesAnyPunchLikeItsWholeMinute()
    {
        var random = new Random(20261026);
        for (var i = 0; i < 20000; i++)
        {
            var clockIn = new TimeOnly(random.Next(6, 23), random.Next(60));
            var clockOut = clockIn.AddMinutes(random.Next(1, (int)(new TimeSpan(23, 59, 0) - clockIn.ToTimeSpan()).TotalMinutes + 1));
            if (clockOut <= clockIn) continue;
            FormPeriod[] forms = random.Next(3) == 0 ? [Approved(At(8, 30), At(8, 30).AddMinutes(random.Next(1, 600)))] : [];
            var exact = EvaluateByMinute(At(clockIn.Hour, clockIn.Minute, random.Next(60)), At(clockOut.Hour, clockOut.Minute, random.Next(60)), forms);
            var whole = EvaluateByMinute(At(clockIn.Hour, clockIn.Minute), At(clockOut.Hour, clockOut.Minute), forms);
            // Gaps 是清單，record 的相等只比參考，另外逐筆比。
            Assert.Equal(whole with { ClockIn = exact.ClockIn, ClockOut = exact.ClockOut, Gaps = exact.Gaps }, exact);
            Assert.Equal(whole.Gaps, exact.Gaps);
            // 刷卡本來就是整分時，整分與以秒計沒有差別。
            var bySecond = Evaluate(At(clockIn.Hour, clockIn.Minute), At(clockOut.Hour, clockOut.Minute), forms: forms);
            Assert.Equal(whole with { Gaps = bySecond.Gaps }, bySecond);
            Assert.Equal(whole.Gaps, bySecond.Gaps);
        }
    }

    [Fact]
    public void PunchesInAnotherOffsetAreReadAsTaipeiTimeAndOrderDoesNotMatter()
    {
        var result = Evaluate(At(18, 0).ToUniversalTime(), At(8, 30).ToUniversalTime());

        AssertPresent(result, At(18, 0), 0, 0);
        Assert.Equal(TimeSpan.FromHours(8), result.ClockIn!.Value.Offset);
    }

    [Fact]
    public void TodayIsNeverAnAnomalyAndNotYetJudgedForLateness()
    {
        foreach (var (first, last) in new (DateTimeOffset?, DateTimeOffset?)[] { (null, null), (At(9, 30), null), (At(9, 30), At(12, 0)) })
        {
            var result = AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, first, last, []), Rules, Day);

            Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayInProgress), (result.Status, result.Reason));
            Assert.Equal((null, 0, null), (result.Coverage, result.ShortSeconds, result.AdjustedClockOut));
            Assert.Equal((first, null, 0, 0), (result.ClockIn, result.ExpectedClockOut, result.LateSeconds, result.EarlyLeaveSeconds));
        }
    }

    [Fact]
    public void TodayKeepsStatesThatAreNotJudgements()
    {
        Assert.Equal(AttendanceStatus.OnLeave, AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, null, null, [Approved(At(8, 30), At(18, 0))]), Rules, Day).Status);
        Assert.Equal(AttendanceStatus.DayOff, AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.DayOff, null, null, []), Rules, Day).Status);
        var unknown = AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.Unknown, null, null, []), Rules, Day);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayTypeUnknown), (unknown.Status, unknown.Reason));
    }

    [Fact]
    public void PastDaysAreJudgedAsUsualAndFutureDaysAreRefused()
    {
        var tomorrow = Day.AddDays(1);

        AssertPresent(AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, At(9, 5), At(18, 0), []), Rules, tomorrow), At(18, 0), Minutes(5), 0);
        Assert.Equal(AttendanceStatus.Anomaly, AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, null, null, []), Rules, tomorrow).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => AttendanceEvaluator.Evaluate(new(tomorrow, AttendanceDayType.FullDay, null, null, []), Rules, Day));
    }

    [Theory]
    [InlineData(false, false, AttendanceDayType.FullDay)]
    [InlineData(true, false, AttendanceDayType.MorningOff)]
    [InlineData(false, true, AttendanceDayType.AfternoonOff)]
    [InlineData(true, true, AttendanceDayType.DayOff)]
    public void TheDayTypeFollowsTheCalendarHalves(bool morningOff, bool afternoonOff, AttendanceDayType expected)
        => Assert.Equal(expected, AttendanceEvaluator.DayTypeOf(Day, new(Day, morningOff, afternoonOff, null)));

    [Fact]
    public void ADateMissingFromTheCalendarIsADayOffOnWeekendsAndUnknownOnWeekdays()
    {
        Assert.Equal(AttendanceDayType.Unknown, AttendanceEvaluator.DayTypeOf(Day, null));
        Assert.Equal(AttendanceDayType.DayOff, AttendanceEvaluator.DayTypeOf(new(2026, 10, 24), null));
        Assert.Equal(AttendanceDayType.DayOff, AttendanceEvaluator.DayTypeOf(new(2026, 10, 25), null));
        // 補班的週六在行事曆上有列，依列判定。
        Assert.Equal(AttendanceDayType.FullDay, AttendanceEvaluator.DayTypeOf(new(2026, 10, 24), new CalendarDayDto(new(2026, 10, 24), false, false, "補班")));
    }
}
