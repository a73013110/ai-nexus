using System.Globalization;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>出勤判定規則的說明：每種日型的上下班時間，加上逐條規則。出勤紀錄頁與 Excel 匯出共用同一份。</summary>
public sealed record AttendanceRulesDto(IReadOnlyList<AttendanceScheduleDto> Schedules, IReadOnlyList<AttendanceRuleDto> Rules);

/// <summary>一種日型目前的設定（台灣時間）：表定上下班、彈性上班區間與應下班的時數（含午休）。整天放假不判定，沒有這一列。</summary>
public sealed record AttendanceScheduleDto(AttendanceDayType DayType, TimeOnly ScheduledStart, TimeOnly ScheduledEnd, TimeOnly FlexStart, TimeOnly FlexEnd, double WorkHours);

/// <summary>一條規則：短標題與一段說明。</summary>
public sealed record AttendanceRuleDto(string Title, string Text);

/// <summary>
/// 出勤規則的說明文字只寫在這裡（ADR 0018「出勤判定」）：出勤紀錄頁（<c>GET /api/v1/attendance-rules</c>）與匯出的「說明」工作表都由此產生，
/// 時間與時數取自目前的 <see cref="WorkdayOptions"/>，改了設定畫面與匯出跟著變，不會與判定脫節。
/// 條文對應 <see cref="AttendanceEvaluator"/> 與 <see cref="ListAttendanceRecords"/> 的實際行為；改判定時一併改這裡，
/// 舉例的時間由 <see cref="AttendanceEvaluator"/> 算出（<c>AttendanceRulesTests</c> 檢查兩者一致）。
/// </summary>
public static class AttendanceRules
{
    public static AttendanceRulesDto Describe(WorkdayOptions options)
    {
        AttendanceScheduleDto Schedule(AttendanceDayType type, WorkdayRule rule) => new(type, rule.ScheduledStart, rule.ScheduledEnd, rule.FlexStart, rule.FlexEnd, rule.WorkHours);
        var full = options.FullDay;
        // 設定驗證保證彈性終點＋時數不跨午夜，所以這兩個例子都落在同一天。
        var earlyArrival = full.FlexStart >= new TimeOnly(0, 10) ? full.FlexStart.AddMinutes(-10) : full.FlexStart;
        var lateArrival = full.FlexEnd.AddMinutes(10);
        // 補足的例子（全天）：09:01:19 到、請 08:30～09:00，扣表單後 18:01:19 可下班；遲到 10 分、表定下班就走，要補的不只 10 分。
        var slightlyLate = full.FlexEnd.Add(new TimeSpan(0, 1, 19));
        var halfHour = full.ScheduledStart.AddMinutes(30);
        var netHours = full.WorkHours - full.BreakLength.TotalHours;
        var lunch = full.BreakStart is { } breakStart && full.BreakEnd is { } breakEnd ? $"午休 {Time(breakStart)}～{Time(breakEnd)} 不算在內。\n" : "";

        return new(
            [Schedule(AttendanceDayType.FullDay, full), Schedule(AttendanceDayType.MorningOff, options.MorningOff), Schedule(AttendanceDayType.AfternoonOff, options.AfternoonOff)],
            [
                new("上班與下班", "當天最早一筆刷卡是上班，最晚一筆是下班，中間的刷卡不影響判定。\n出勤日依刷卡時間的日期，不看打卡檔的檔名；過了午夜的刷卡算隔天。"),
                new("彈性上班", "在彈性時段內上班不算遲到。\n應下班＝上班時間＋規定時數（含午休）；早於彈性起點上班，以起點計。\n" +
                    $"例：上班日 {Time(full.FlexStart)} 前到，應下班 {Time(ExpectedClockOut(earlyArrival, options))}。\n" +
                    $"例：{Time(full.ScheduledStart)} 到，應下班 {Time(ExpectedClockOut(full.ScheduledStart, options))}。"),
                new("遲到", "晚於彈性終點上班就是遲到，從彈性終點起算。\n遲到時，應下班改為表定下班。\n" +
                    $"例：上班日 {Time(lateArrival)} 到，遲到 10 分，應下班 {Time(ExpectedClockOut(lateArrival, options))}。"),
                new("早退", "早於應下班離開就是早退。\n" + (options.PunchPrecision == PunchPrecision.Minute
                    ? $"遲到與早退以整分計：刷卡的秒數先捨去再比（{Time(full.FlexEnd)}:59 到不算遲到）。"
                    : "遲到與早退都以秒計，彈性時段以外沒有寬限。")),
                new("只有一筆刷卡", "判為異常。\n分不出缺的是上班還是下班，所以不算遲到或早退。"),
                new("沒有刷卡", "核准的假單或出差單補足當天工時：算請假。\n沒有表單、只請了部分時段，或表單還在流程中：算異常。"),
                new("假單與出差單", "取公文系統已核准與流程中的表單；與當天有交集就列在當天，跨日、半天都一樣。\n暫存、退回、銷單與退回結案不算。\n退回結案是承辦人把退回的單結案存查，不是核准。"),
                new("表單補足", "表單只比時數，不比時段。\n" +
                    $"核准表單的時數，從當天工時（上班日 {Hours(netHours)} 小時）扣掉；剩下的不多於實際上班的時數，就算補足。\n" +
                    $"遲到時，表單至少要抵掉遲到的部分，晚走不抵遲到。\n{lunch}只算彈性起點到最晚應下班之間，重疊的表單只算一次。\n" +
                    $"例：上班日 {Seconds(slightlyLate)} 到、請 {Time(full.ScheduledStart)}～{Time(halfHour)} 的假，" +
                    $"扣表單後應下班 {Seconds(AdjustedClockOut(slightlyLate, full.ScheduledStart, halfHour, options))}。\n" +
                    $"例：{Time(lateArrival)} 到、{Time(full.ScheduledEnd)} 走，遲到 10 分，但要 {Span(Short(lateArrival, full.ScheduledEnd, options))}的表單才補足（上班時數少了這麼多）。"),
                new("補足狀態", "有遲到、早退或異常才標。\n" +
                    $"・{Label(GapCoverage.Covered)}：核准的表單就夠\n" +
                    $"・{Label(GapCoverage.Pending)}：加上流程中的表單才夠\n" +
                    $"・{Label(GapCoverage.Unexplained)}：都不夠，並寫出還差多少\n" +
                    "遲到、早退的時間與小計照原本的判定，不因表單改變。"),
                new("日型", "上班日、上午放假、下午放假與放假，依公文系統的假日行事曆。\n放假整天不判定，刷卡照樣列出。\n行事曆沒有這一天時：週末當放假，平日無法判定。"),
                new("無法判定", "公文系統連不上或對不到公文帳號時，表單無法取得；沒刷卡的日子列為無法判定，不算異常。\n" +
                    "今天還沒結束（打卡檔晚上才匯入）時，出勤與異常一律先列為無法判定，也不算遲到。"),
                new("列出的日子", "只列持有卡的日子。\n到職前、換卡空窗、離職後與未來的日子都不列。"),
            ]);
    }

    /// <summary>「上班＋N 小時」的 N；整數不帶小數點。</summary>
    public static string Hours(double hours) => hours.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Seconds(TimeOnly time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>補足狀態的字樣，與畫面相同。</summary>
    public static string Label(GapCoverage? coverage) => coverage switch
    {
        GapCoverage.Covered => "已補足",
        GapCoverage.Pending => "流程中",
        GapCoverage.Unexplained => "未補足",
        GapCoverage.Unknown => "表單無法取得",
        _ => "沒有缺口"
    };

    /// <summary>「1 小時 30 分」「40 分」這類時長；說明裡的例子都是整分。</summary>
    private static string Span(int seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return ((span.Hours > 0 ? $"{span.Hours} 小時 " : "") + (span.Minutes > 0 ? $"{span.Minutes} 分" : "")).TrimEnd();
    }

    // 以下舉例都由判定本身算出，說明的數字才不會與判定各算各的。日期是任意一個上班日。
    private static readonly DateOnly ExampleDate = new(2026, 1, 5);

    private static AttendanceEvaluation Example(TimeOnly arrival, TimeOnly leaving, WorkdayOptions options, params FormPeriod[] forms)
        => AttendanceEvaluator.Evaluate(new(ExampleDate, AttendanceDayType.FullDay, TaipeiTime.At(ExampleDate, arrival), TaipeiTime.At(ExampleDate, leaving), forms), options);

    private static TimeOnly ClockTime(DateTimeOffset? value) => TimeOnly.FromTimeSpan(TaipeiTime.Of(value!.Value).TimeOfDay);

    /// <summary>某個上班時間的應下班（不看表單）。</summary>
    private static TimeOnly ExpectedClockOut(TimeOnly arrival, WorkdayOptions options)
        => ClockTime(Example(arrival, new TimeOnly(23, 59, 59), options).ExpectedClockOut);

    /// <summary>某時間到、請了一段核准的假，扣表單後的應下班。</summary>
    private static TimeOnly AdjustedClockOut(TimeOnly arrival, TimeOnly formStart, TimeOnly formEnd, WorkdayOptions options)
        => ClockTime(Example(arrival, new TimeOnly(23, 59, 59), options,
            new FormPeriod(TaipeiTime.At(ExampleDate, formStart), TaipeiTime.At(ExampleDate, formEnd), Approved: true)).AdjustedClockOut);

    /// <summary>沒有表單時還差多少秒的表單。</summary>
    private static int Short(TimeOnly arrival, TimeOnly leaving, WorkdayOptions options) => Example(arrival, leaving, options).ShortSeconds;
}
