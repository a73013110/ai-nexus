using AiNexus.Features.Staff.Attendance.Calendar;

namespace AiNexus.Features.Staff.Attendance.Records;

/// <summary>
/// 出勤判定（ADR 0018「出勤判定」）：純函式，只看輸入與規則設定，不讀時鐘也不查資料庫，查詢時逐日計算、不存檔。
/// 不帶今天的多載一律當成已結束的一天；查詢端點用帶 <c>today</c> 的多載，今天不判成異常。
/// </summary>
public static class AttendanceEvaluator
{
    /// <summary>
    /// 由 GD 行事曆推得日型。GD 沒有這天（或連不上時傳 null）：週六、週日視為整天放假，平日為 <see cref="AttendanceDayType.Unknown"/>。
    /// 無法查證 GD 是否每個週末都有列；週末缺列時當放假最不會誤判（補班日在 GD 會有列且 AM、PM 都不放假），平日缺列則不猜。
    /// </summary>
    public static AttendanceDayType DayTypeOf(DateOnly date, CalendarDayDto? day) => day switch
    {
        null => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? AttendanceDayType.DayOff : AttendanceDayType.Unknown,
        { MorningOff: true, AfternoonOff: true } => AttendanceDayType.DayOff,
        { MorningOff: true } => AttendanceDayType.MorningOff,
        { AfternoonOff: true } => AttendanceDayType.AfternoonOff,
        _ => AttendanceDayType.FullDay
    };

    /// <summary>
    /// 以 <paramref name="today"/>（台灣日期）為準判定：過去的日子同 <see cref="Evaluate(AttendanceDay, WorkdayOptions)"/>；
    /// 今天的出勤與異常改為 <see cref="AttendanceStatus.Undetermined"/>（<see cref="AttendanceReason.DayInProgress"/>），刷卡照樣回傳。
    /// 今天的檔通常晚上才匯入，白天看到的「沒刷卡」「只有上班」都不是結論；連遲到也不算，因為中午的快照之後還可能被晚上的檔取代。
    /// 放假、請假與其他無法判定的狀態本來就不是異常，維持原樣。未來的日子沒有可判定的東西，呼叫端不要傳進來（拋出）。
    /// </summary>
    public static AttendanceEvaluation Evaluate(AttendanceDay day, WorkdayOptions rules, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day.Date, today);
        var result = Evaluate(day, rules);
        return day.Date == today && result.Status is AttendanceStatus.Present or AttendanceStatus.Anomaly
            ? result with { Status = AttendanceStatus.Undetermined, Reason = AttendanceReason.DayInProgress, ExpectedClockOut = null, LateSeconds = 0, EarlyLeaveSeconds = 0 }
            : result;
    }

    public static AttendanceEvaluation Evaluate(AttendanceDay day, WorkdayOptions rules)
    {
        var (clockIn, clockOut) = Punches(day.FirstPunch, day.LastPunch);
        AttendanceEvaluation Unjudged(AttendanceStatus status, AttendanceReason? reason = null)
            => new(day.Date, day.DayType, status, reason, clockIn, clockOut, null, 0, 0, day.Leave);

        if (day.DayType == AttendanceDayType.DayOff) return Unjudged(AttendanceStatus.DayOff);
        var rule = day.DayType switch
        {
            AttendanceDayType.FullDay => rules.FullDay,
            AttendanceDayType.MorningOff => rules.MorningOff,
            AttendanceDayType.AfternoonOff => rules.AfternoonOff,
            _ => null
        };
        if (rule is null) return Unjudged(AttendanceStatus.Undetermined, AttendanceReason.DayTypeUnknown);

        if (clockIn is null || clockOut is null)
        {
            return day.Leave switch
            {
                LeaveState.Filed => Unjudged(AttendanceStatus.OnLeave),
                // 公文系統連不上時每天都會「沒有假單」，判成異常就是整頁誤報（ADR 0018）。
                LeaveState.Unavailable => Unjudged(AttendanceStatus.Undetermined, AttendanceReason.LeaveUnavailable),
                _ => Unjudged(AttendanceStatus.Anomaly, AttendanceReason.NoPunchOrLeave)
            };
        }

        // 只有一筆刷卡（打卡檔每張卡只留最早與最晚，兩者相同＝只刷一次）：少了上班或下班其中一次，判異常；
        // 但分不出缺的是哪一次，當成上班會判出大段早退、當成下班會判出大段遲到，所以不算遲到早退，刷卡時間照樣回傳。
        if (clockIn == clockOut) return Unjudged(AttendanceStatus.Anomaly, AttendanceReason.SinglePunch);

        var flexStart = TaipeiTime.At(day.Date, rule.FlexStart);
        var flexEnd = TaipeiTime.At(day.Date, rule.FlexEnd);
        var late = clockIn.Value > flexEnd;
        // 遲到從彈性終點起算：彈性內到都不算遲到，超過的部分才是遲到。
        var lateSeconds = late ? Seconds(clockIn.Value - flexEnd) : 0;
        // 時數換成整數秒，避免 8.2 這類小數小時在 double 下差幾個 tick 而多出一秒早退。
        var expected = late
            ? TaipeiTime.At(day.Date, rule.ScheduledEnd)
            : (clockIn.Value < flexStart ? flexStart : clockIn.Value).AddSeconds(Math.Round(rule.WorkHours * 3600));
        var earlySeconds = clockOut.Value < expected ? Seconds(expected - clockOut.Value) : 0;

        return new(day.Date, day.DayType, AttendanceStatus.Present, null, clockIn, clockOut, expected, lateSeconds, earlySeconds, day.Leave);
    }

    /// <summary>
    /// 最早為上班、最晚為下班，換成 +08:00 並捨去不足一秒的部分（打卡檔精確到秒；資料庫值若帶小數秒，同一秒的兩筆仍算一筆）。
    /// 只給了一端時兩者相同，視為一筆刷卡。
    /// </summary>
    private static (DateTimeOffset? In, DateTimeOffset? Out) Punches(DateTimeOffset? first, DateTimeOffset? last)
    {
        if (first is null && last is null) return (null, null);
        var a = Truncate(first ?? last!.Value);
        var b = Truncate(last ?? first!.Value);
        return a <= b ? (a, b) : (b, a);
    }

    private static DateTimeOffset Truncate(DateTimeOffset value)
    {
        var local = TaipeiTime.Of(value);
        return local.AddTicks(-(local.Ticks % TimeSpan.TicksPerSecond));
    }

    /// <summary>以秒計、不足一秒捨去。</summary>
    private static int Seconds(TimeSpan span) => (int)Math.Floor(span.TotalSeconds);
}
