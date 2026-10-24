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

        return new(
            [Schedule(AttendanceDayType.FullDay, full), Schedule(AttendanceDayType.MorningOff, options.MorningOff), Schedule(AttendanceDayType.AfternoonOff, options.AfternoonOff)],
            [
                new("上班與下班", "當天最早一筆刷卡為上班、最晚一筆為下班，中間的刷卡不影響判定。出勤日依刷卡時間的日期，不依打卡檔的檔名：過了午夜的刷卡算隔天。"),
                new("彈性上班", $"彈性時段內上班不算遲到，應下班＝上班＋規定時數（含午休）；早於彈性起點上班以起點計。" +
                    $"例：上班日 {Time(full.FlexStart)} 前到，應下班 {Time(ExpectedClockOut(earlyArrival, options))}；" +
                    $"{Time(full.ScheduledStart)} 到，應下班 {Time(ExpectedClockOut(full.ScheduledStart, options))}。"),
                new("遲到", $"晚於彈性終點上班為遲到，從彈性終點起算；遲到時應下班改為表定下班。" +
                    $"例：上班日 {Time(lateArrival)} 到，遲到 10 分，應下班 {Time(ExpectedClockOut(lateArrival, options))}。"),
                new("早退", "早於應下班離開為早退。遲到與早退都以秒計，彈性時段以外沒有寬限。"),
                new("只有一筆刷卡", "判為異常；分不出缺的是上班還是下班，所以不算遲到早退。"),
                new("沒有刷卡", "當天有假單或出差單為請假，都沒有為異常。"),
                new("假單與出差單", "取公文系統已送出的單據（暫存、退回、取消不算），與當天有交集就列在當天，跨日、半天都一樣。" +
                    "只並列、不抵銷遲到早退，也不比對時數，由人事判斷：上午請假、下午才到照樣算遲到。"),
                new("日型", "上班日、上午放假、下午放假與放假依公文系統的假日行事曆；放假整天不判定，刷卡照樣列出。行事曆沒有這一天時，週末當放假、平日無法判定。"),
                new("無法判定", "公文系統連不上或對不到公文帳號時假單無法取得，沒刷卡的日子列為無法判定，不算異常。" +
                    "今天還沒結束（打卡檔晚上才匯入），出勤與異常一律先列為無法判定，也不算遲到。"),
                new("列出的日子", "只列持有卡的日子：到職前、換卡空窗與離職後不列，未來的日子也不列。"),
            ]);
    }

    /// <summary>「上班＋N 小時」的 N；整數不帶小數點。</summary>
    public static string Hours(double hours) => hours.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>舉例用：由判定本身算出某個上班時間的應下班，說明的數字才不會與判定各算各的。</summary>
    private static TimeOnly ExpectedClockOut(TimeOnly arrival, WorkdayOptions options)
    {
        var date = new DateOnly(2026, 1, 5);
        var day = new AttendanceDay(date, AttendanceDayType.FullDay, TaipeiTime.At(date, arrival), TaipeiTime.At(date, new TimeOnly(23, 59, 59)), LeaveState.None);
        var expected = AttendanceEvaluator.Evaluate(day, options).ExpectedClockOut!.Value;
        return TimeOnly.FromTimeSpan(TaipeiTime.Of(expected).TimeOfDay);
    }
}
