using AiNexus.Features.Staff.Attendance;
using AiNexus.Features.Staff.Attendance.Calendar;
using AiNexus.Features.Staff.Attendance.Records;

namespace AiNexus.UnitTests.Attendance;

public sealed class AttendanceEvaluatorTests
{
    /// <summary>週三，平日。</summary>
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static readonly WorkdayOptions Rules = new();

    private static DateTimeOffset At(int hour, int minute, int second = 0) => TaipeiTime.At(Day, new TimeOnly(hour, minute, second));

    private static AttendanceEvaluation Evaluate(DateTimeOffset? first, DateTimeOffset? last, AttendanceDayType type = AttendanceDayType.FullDay, LeaveState leave = LeaveState.None)
        => AttendanceEvaluator.Evaluate(new(Day, type, first, last, leave), Rules);

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

    [Fact]
    public void AMorningLeaveDoesNotOffsetLatenessButIsShownAlongside()
    {
        var result = Evaluate(At(13, 30), At(18, 0), leave: LeaveState.Filed);

        AssertPresent(result, At(18, 0), Minutes(270), 0);
        Assert.Equal(LeaveState.Filed, result.Leave);
    }

    [Fact]
    public void NoPunchWithoutLeaveIsAnAnomalyButWithLeaveIsOnLeave()
    {
        var anomaly = Evaluate(null, null);
        Assert.Equal((AttendanceStatus.Anomaly, AttendanceReason.NoPunchOrLeave), (anomaly.Status, anomaly.Reason));
        Assert.Null(anomaly.ExpectedClockOut);

        var onLeave = Evaluate(null, null, leave: LeaveState.Filed);
        Assert.Equal((AttendanceStatus.OnLeave, (AttendanceReason?)null), (onLeave.Status, onLeave.Reason));
    }

    [Fact]
    public void UnavailableLeaveIsUndeterminedInsteadOfAnAnomalyAndDoesNotBlockPunchJudgement()
    {
        var missing = Evaluate(null, null, leave: LeaveState.Unavailable);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.LeaveUnavailable), (missing.Status, missing.Reason));

        var present = Evaluate(At(9, 10), At(18, 0), leave: LeaveState.Unavailable);
        AssertPresent(present, At(18, 0), Minutes(10), 0);
        Assert.Equal(LeaveState.Unavailable, present.Leave);
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
            var result = AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, first, last, LeaveState.None), Rules, Day);

            Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayInProgress), (result.Status, result.Reason));
            Assert.Equal((first, null, 0, 0), (result.ClockIn, result.ExpectedClockOut, result.LateSeconds, result.EarlyLeaveSeconds));
        }
    }

    [Fact]
    public void TodayKeepsStatesThatAreNotJudgements()
    {
        Assert.Equal(AttendanceStatus.OnLeave, AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, null, null, LeaveState.Filed), Rules, Day).Status);
        Assert.Equal(AttendanceStatus.DayOff, AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.DayOff, null, null, LeaveState.None), Rules, Day).Status);
        var unknown = AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.Unknown, null, null, LeaveState.None), Rules, Day);
        Assert.Equal((AttendanceStatus.Undetermined, AttendanceReason.DayTypeUnknown), (unknown.Status, unknown.Reason));
    }

    [Fact]
    public void PastDaysAreJudgedAsUsualAndFutureDaysAreRefused()
    {
        var tomorrow = Day.AddDays(1);

        AssertPresent(AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, At(9, 5), At(18, 0), LeaveState.None), Rules, tomorrow), At(18, 0), Minutes(5), 0);
        Assert.Equal(AttendanceStatus.Anomaly, AttendanceEvaluator.Evaluate(new(Day, AttendanceDayType.FullDay, null, null, LeaveState.None), Rules, tomorrow).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => AttendanceEvaluator.Evaluate(new(tomorrow, AttendanceDayType.FullDay, null, null, LeaveState.None), Rules, Day));
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
