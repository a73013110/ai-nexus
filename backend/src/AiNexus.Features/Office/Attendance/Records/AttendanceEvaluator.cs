using AiNexus.Features.Office.Attendance.Calendar;

namespace AiNexus.Features.Office.Attendance.Records;

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
    /// 今天的出勤與異常改為 <see cref="AttendanceStatus.Undetermined"/>（<see cref="AttendanceReason.DayInProgress"/>），刷卡與表單時數照樣回傳。
    /// 今天的檔通常晚上才匯入，白天看到的「沒刷卡」「只有上班」都不是結論；連遲到也不算，因為中午的快照之後還可能被晚上的檔取代。
    /// 放假、請假與其他無法判定的狀態本來就不是異常，維持原樣。未來的日子沒有可判定的東西，呼叫端不要傳進來（拋出）。
    /// </summary>
    public static AttendanceEvaluation Evaluate(AttendanceDay day, WorkdayOptions rules, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day.Date, today);
        var result = Evaluate(day, rules);
        return day.Date == today && result.Status is AttendanceStatus.Present or AttendanceStatus.Anomaly
            ? result with
            {
                Status = AttendanceStatus.Undetermined, Reason = AttendanceReason.DayInProgress, ExpectedClockOut = null, LateSeconds = 0, EarlyLeaveSeconds = 0,
                AdjustedClockOut = null, Coverage = null, ShortSeconds = 0, Gaps = []
            }
            : result;
    }

    /// <summary>判定一天：先照舊系統算遲到、早退與應下班，再看表單補不補得足（<see cref="GapCoverage"/>）。</summary>
    /// <remarks>
    /// 表單的規則只寫在 <see cref="Shift.Need"/>（這天要多少秒的表單）與 <see cref="Shift.ClockOutWith"/>（扣表單後的應下班），改規則改這兩處：
    /// <list type="bullet">
    /// <item>表單比時數、不比區間：核准表單的時數直接從當天該上的工時扣掉，表單不必蓋在缺口上（09:01 到、請 08:30～09:00，等於 08:31 到）。</item>
    /// <item>補足＝扣掉表單後的工時不多於實際上班時數，且表單至少抵掉彈性終點後的遲到：晚走不抵遲到（同舊系統遲到照表定下班）。</item>
    /// <item>原始的遲到、早退、應下班不因表單改變（小計同舊系統）；有缺口才判補足，核准的夠為已補足、加上簽核中的才夠為簽核中、都不夠為未補足。</item>
    /// <item>整天沒刷卡而核准的表單抵掉整天就是請假（<see cref="AttendanceStatus.OnLeave"/>），不算異常；只有一筆刷卡仍是異常，上班時數當成 0。</item>
    /// </list>
    /// </remarks>
    public static AttendanceEvaluation Evaluate(AttendanceDay day, WorkdayOptions rules)
    {
        var (clockIn, clockOut) = Punches(day.FirstPunch, day.LastPunch);
        var unjudged = new AttendanceEvaluation(day.Date, day.DayType, AttendanceStatus.Undetermined, null, clockIn, clockOut, null, 0, 0, day.FormState);

        if (day.DayType == AttendanceDayType.DayOff) return unjudged with { Status = AttendanceStatus.DayOff };
        var rule = day.DayType switch
        {
            AttendanceDayType.FullDay => rules.FullDay,
            AttendanceDayType.MorningOff => rules.MorningOff,
            AttendanceDayType.AfternoonOff => rules.AfternoonOff,
            _ => null
        };
        if (rule is null) return unjudged with { Reason = AttendanceReason.DayTypeUnknown };
        // 公文系統連不上時每天都會「沒有假單」，判成異常就是整頁誤報（ADR 0018）。
        if (clockIn is null && day.Forms is null) return unjudged with { Reason = AttendanceReason.FormsUnavailable };

        var shift = new Shift(day.Date, rule);
        var need = shift.Need(clockIn, clockOut);
        var approved = day.Forms is { } forms ? shift.FormSeconds(forms.Where(x => x.Approved)) : 0;
        int? filed = day.Forms is { } all ? shift.FormSeconds(all) : null;
        GapCoverage? coverage = need == 0 ? null
            : filed is null ? GapCoverage.Unknown
            : approved >= need ? GapCoverage.Covered
            : filed >= need ? GapCoverage.Pending
            : GapCoverage.Unexplained;
        var judged = unjudged with
        {
            FormSeconds = approved, Coverage = coverage, ShortSeconds = coverage is GapCoverage.Unexplained or GapCoverage.Pending ? need - approved : 0
        };

        if (clockIn is null || clockOut is null)
            return coverage == GapCoverage.Covered
                ? judged with { Status = AttendanceStatus.OnLeave, Coverage = null }
                : judged with { Status = AttendanceStatus.Anomaly, Reason = AttendanceReason.NoPunchOrLeave, Gaps = [new(AttendanceGapKind.Absent, shift.ScheduledStart, shift.ScheduledEnd)] };

        // 只有一筆刷卡（打卡檔每張卡只留最早與最晚，兩者相同＝只刷一次）：少了上班或下班其中一次，判異常；
        // 但分不出缺的是哪一次，當成上班會判出大段早退、當成下班會判出大段遲到，所以不算遲到早退，刷卡時間照樣回傳。
        if (clockIn == clockOut) return judged with { Status = AttendanceStatus.Anomaly, Reason = AttendanceReason.SinglePunch };

        // 遲到從彈性終點起算（彈性內到都不算遲到）；遲到照表定下班，否則上班（早於彈性起點以起點計）＋時數。
        var late = clockIn.Value > shift.FlexEnd;
        var expected = late ? shift.ScheduledEnd : shift.ClockOutWith(clockIn.Value, 0)!.Value;
        var early = clockOut.Value < expected;
        List<AttendanceGap> gaps = [];
        if (late) gaps.Add(new(AttendanceGapKind.Late, shift.FlexEnd, clockIn.Value));
        if (early) gaps.Add(new(AttendanceGapKind.EarlyLeave, clockOut.Value, expected));
        return judged with
        {
            Status = AttendanceStatus.Present, ExpectedClockOut = expected,
            LateSeconds = late ? Seconds(clockIn.Value - shift.FlexEnd) : 0,
            EarlyLeaveSeconds = early ? Seconds(expected - clockOut.Value) : 0,
            AdjustedClockOut = approved > 0 ? shift.ClockOutWith(clockIn.Value, approved) : null,
            Gaps = gaps
        };
    }

    /// <summary>一天套上一種日型的規則：彈性區間、表定下班、午休與淨工時（時數扣午休）。</summary>
    /// <remarks>設定驗證保證午休在彈性終點之後、最早的應下班之前，所以不看表單時 <see cref="ClockOutWith"/> 與舊系統的「上班＋時數」相同。</remarks>
    private sealed class Shift(DateOnly date, WorkdayRule rule)
    {
        private readonly DateTimeOffset? breakStart = rule.BreakStart is { } start ? TaipeiTime.At(date, start) : null;
        private readonly DateTimeOffset? breakEnd = rule.BreakEnd is { } end ? TaipeiTime.At(date, end) : null;
        // 時數換成整數秒，避免 8.2 這類小數小時在 double 下差幾個 tick 而多出一秒早退。
        private readonly int workSeconds = (int)Math.Round(rule.WorkHours * 3600);

        public DateTimeOffset FlexStart { get; } = TaipeiTime.At(date, rule.FlexStart);
        public DateTimeOffset FlexEnd { get; } = TaipeiTime.At(date, rule.FlexEnd);
        public DateTimeOffset ScheduledStart { get; } = TaipeiTime.At(date, rule.ScheduledStart);
        public DateTimeOffset ScheduledEnd { get; } = TaipeiTime.At(date, rule.ScheduledEnd);

        /// <summary>一天該上的淨工時（全天 9.5 小時扣午休 1.5 小時＝8 小時）。</summary>
        private int NetWork => workSeconds - (int)rule.BreakLength.TotalSeconds;

        /// <summary>
        /// 這天要多少秒的核准表單才補足；0＝沒有缺口（與舊系統沒有遲到也沒有早退一致）。
        /// 工時差多少就要多少表單，但至少要抵掉彈性終點後的遲到；最多一整天的淨工時。沒刷卡或只有一筆時，上班時數是 0。
        /// </summary>
        public int Need(DateTimeOffset? clockIn, DateTimeOffset? clockOut)
        {
            if (clockIn is not { } inAt || clockOut is not { } outAt) return NetWork;
            var worked = NetSeconds(Max(inAt, FlexStart), outAt);
            return Math.Min(NetWork, Math.Max(LateNet(inAt), NetWork - worked));
        }

        /// <summary>
        /// 抵掉 <paramref name="formSeconds"/> 秒表單後的應下班：從上班（早於彈性起點以起點計）起再上滿剩下的淨工時，跳過午休。
        /// 表單還不夠抵遲到時仍是遲到、沒有可以補足的下班時間；抵掉整天時幾點走都可以。這兩種為 null。
        /// </summary>
        public DateTimeOffset? ClockOutWith(DateTimeOffset clockIn, int formSeconds)
            => formSeconds < LateNet(clockIn) || formSeconds >= NetWork ? null : Forward(Max(clockIn, FlexStart), NetWork - formSeconds);

        /// <summary>
        /// 表單抵掉的淨秒數（最多一天的淨工時）：截到彈性起點～最晚的應下班（彈性終點上班＋時數，與表定下班取晚者），合併重疊、扣午休。
        /// 只截掉不可能上班的時段，人在公司期間的表單照樣算，表單本來就只比時數。
        /// </summary>
        public int FormSeconds(IEnumerable<FormPeriod> periods)
        {
            var (from, to) = (FlexStart, Max(FlexEnd.AddSeconds(workSeconds), ScheduledEnd));
            var total = 0;
            DateTimeOffset? start = null, end = null;
            foreach (var (s, e) in periods.Select(x => (Start: Max(x.StartsAt, from), End: Min(x.EndsAt, to))).Where(x => x.Start < x.End).OrderBy(x => x.Start))
            {
                if (end is { } last && s <= last)
                {
                    end = Max(last, e);
                    continue;
                }
                if (start is { } a && end is { } b) total += NetSeconds(a, b);
                (start, end) = (s, e);
            }
            if (start is { } x && end is { } y) total += NetSeconds(x, y);
            // 超過一整天的部分（跨日的出差單）沒有用處，畫面也只該顯示到一天。
            return Math.Min(total, NetWork);
        }

        /// <summary>彈性終點以後的遲到淨秒數（扣午休：中午才到不會多算 1.5 小時）。</summary>
        private int LateNet(DateTimeOffset clockIn) => clockIn > FlexEnd ? NetSeconds(FlexEnd, clockIn) : 0;

        /// <summary><paramref name="from"/>～<paramref name="to"/> 扣掉午休的秒數。</summary>
        private int NetSeconds(DateTimeOffset from, DateTimeOffset to)
        {
            if (to <= from) return 0;
            var span = to - from;
            if (breakStart is { } start && breakEnd is { } end && Min(to, end) > Max(from, start)) span -= Min(to, end) - Max(from, start);
            return Seconds(span);
        }

        /// <summary><see cref="NetSeconds"/> 的反函數：從 <paramref name="from"/> 起上班 <paramref name="seconds"/> 秒（跳過午休）的時間。</summary>
        private DateTimeOffset Forward(DateTimeOffset from, int seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            if (breakStart is not { } start || breakEnd is not { } end || from >= end) return from + span;
            // 午休中才開始的從午休結束起算；午休前開始的，超過午休起點的部分移到午休後。
            if (from >= start) return end + span;
            return span <= start - from ? from + span : end + (span - (start - from));
        }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;

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
