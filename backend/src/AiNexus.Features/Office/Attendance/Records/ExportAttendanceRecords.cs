using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Attendance.Leave;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄匯出 Excel：條件、區間上限、資料範圍與判定都直接沿用 <see cref="ListAttendanceRecords"/>（同一次查詢、同一個驗證），
/// 只把結果排成工作表，看不到的人也匯不出來。排版參考舊系統 EmpClock 的「出缺勤計算」，改成可篩選的平面表格：
/// <list type="bullet">
/// <item>「出勤紀錄」：每人每天一列，姓名與帳號每列都有（舊系統只寫在第一列，篩選後就看不出是誰）；遲到、早退與異常以深紅色標示，列印時每人一頁。</item>
/// <item>「統計」：每人一列的天數與遲到、早退合計，取代舊系統塞在姓名欄下方的「遲到(計)」等小計列。</item>
/// <item>「說明」：查詢條件、資料來源狀態與判定規則（<see cref="AttendanceRules"/>，與頁面同一份）。</item>
/// </list>
/// 匯出是把個人出勤資料帶離系統，先寫 <see cref="Action"/> 讀取稽核再產生檔案，寫不進去就不匯出（與稽核、日誌匯出相同）。
/// </summary>
internal sealed class ExportAttendanceRecords(ListAttendanceRecords records, AppDbContext db, IOptions<AttendanceOptions> options, TimeProvider clock)
{
    public const string Action = "attendance.records.export";

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/export", ([AsParameters] AttendanceRecordsFilter filter, ICurrentUser user, ExportAttendanceRecords handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, filter, ct)
                .ToHttpResultAsync(file => TypedResults.File(file.Content, Spreadsheet.ContentType, file.Name)))
        .RequireRateLimiting(AttendanceModule.ExportRateLimit)
        .WithName("ExportAttendanceRecords");

    /// <summary>檔名含查詢區間與匯出時間（台灣時間），對應舊系統的「出缺勤計算_匯出時間(起to迄).xlsx」。</summary>
    internal sealed record ExportedFile(string Name, byte[] Content);

    public async Task<Result<ExportedFile>> HandleAsync(Guid viewer, AttendanceRecordsFilter filter, CancellationToken ct)
    {
        // 不分頁：匯出整份名單，區間上限與畫面相同（一年）。
        var read = await records.HandleAsync(viewer, filter, ListAttendanceRecords.Page.All, ct);
        if (!read.IsSuccess) return read.Error;
        var data = read.Value;
        // 稽核記查詢條件與人數，不記名單本身；寫入失敗時 SaveChanges 拋例外，檔案不會產生（失敗即拒絕）。
        db.AuditEvents.Add(new()
        {
            OwnerId = viewer, Action = Action, Result = "read",
            DetailsJson = JsonSerializer.Serialize(new
            {
                from = data.From, to = data.To, userId = filter.UserId, cardNumber = filter.CardNumber, unitId = filter.UnitId,
                includeExcluded = filter.IncludeExcluded == true, includeDaysOff = filter.IncludeDaysOff == true,
                includeUnattributed = filter.IncludeUnattributed == true, count = data.People.Count
            })
        });
        await db.SaveChangesAsync(ct);

        var exportedAt = TaipeiTime.Of(clock.GetUtcNow());
        var rules = AttendanceRules.Describe(options.Value.Workday);
        var content = Spreadsheet.Write([Days(data), Summary(data), Notes(data, rules, exportedAt, filter)]);
        return new ExportedFile(Invariant($"出勤紀錄_{data.From:yyyyMMdd}-{data.To:yyyyMMdd}_{exportedAt:yyyyMMdd_HHmmss}.xlsx"), content);
    }

    private static SpreadsheetSheet Days(AttendanceRecordsDto data)
    {
        var sheet = new SpreadsheetSheet("出勤紀錄",
        [
            new("姓名", 12), new("帳號或卡號", 18), new("日期", 12), new("星期", 6), new("日型", 10), new("行事曆備註", 16), new("狀態", 10),
            new("上班", 10), new("下班", 10), new("應下班", 10), new("遲到", 10), new("早退", 10), new("說明", 34), new("假單與出差", 60)
        ]);
        foreach (var person in data.People)
        {
            sheet.BreakPage();
            foreach (var day in person.Days)
            {
                var late = day.LateSeconds > 0;
                var early = day.EarlyLeaveSeconds > 0;
                // 只有一筆刷卡時上下班是同一筆，與頁面相同只列在上班。
                var singlePunch = day.ClockIn is not null && day.ClockIn == day.ClockOut;
                sheet.Add(
                    person.DisplayName, Identity(person), day.Date, Weekday(day.Date), DayTypes[day.DayType], day.CalendarRemark,
                    new SpreadsheetCell(Statuses[day.Status], day.Status == AttendanceStatus.Anomaly ? SpreadsheetTone.Danger : SpreadsheetTone.Normal),
                    new SpreadsheetCell(Clock(day.ClockIn), late ? SpreadsheetTone.Danger : SpreadsheetTone.Normal),
                    new SpreadsheetCell(singlePunch ? null : Clock(day.ClockOut), early ? SpreadsheetTone.Danger : SpreadsheetTone.Normal),
                    Clock(day.ExpectedClockOut),
                    new SpreadsheetCell(late ? TimeSpan.FromSeconds(day.LateSeconds) : null, SpreadsheetTone.Danger),
                    new SpreadsheetCell(early ? TimeSpan.FromSeconds(day.EarlyLeaveSeconds) : null, SpreadsheetTone.Danger),
                    day.Reason is { } reason ? Reasons[reason] : null,
                    Sheets(day));
            }
        }
        return sheet;
    }

    private static SpreadsheetSheet Summary(AttendanceRecordsDto data)
    {
        var sheet = new SpreadsheetSheet("統計",
        [
            new("姓名", 12), new("帳號或卡號", 18), new("列出天數", 9), new("出勤", 7), new("請假", 7), new("放假", 7), new("異常", 7), new("無法判定", 9),
            new("遲到天數", 9), new("遲到合計", 10), new("早退天數", 9), new("早退合計", 10), new("備註", 40)
        ]);
        foreach (var person in data.People)
        {
            int Count(AttendanceStatus status) => person.Days.Count(x => x.Status == status);
            var anomalies = Count(AttendanceStatus.Anomaly);
            var lateDays = person.Days.Where(x => x.LateSeconds > 0).ToList();
            var earlyDays = person.Days.Where(x => x.EarlyLeaveSeconds > 0).ToList();
            List<string> notes = [];
            if (person.Excluded) notes.Add("不列入出勤");
            if (person.Unattributed) notes.Add("未歸戶卡號：刷卡沒有持有人，只列有刷卡的日子，假單無法取得");
            else if (person.UserId is null) notes.Add("尚未綁帳號（卡號對應只有姓名），假單無法取得");
            else if (!person.LeaveAvailable && data.LeaveAvailable) notes.Add("對不到公文系統帳號，假單無法取得");
            sheet.Add(
                person.DisplayName, Identity(person), person.Days.Count, Count(AttendanceStatus.Present), Count(AttendanceStatus.OnLeave), Count(AttendanceStatus.DayOff),
                new SpreadsheetCell(anomalies, anomalies > 0 ? SpreadsheetTone.Danger : SpreadsheetTone.Normal), Count(AttendanceStatus.Undetermined),
                lateDays.Count, lateDays.Count > 0 ? TimeSpan.FromSeconds(lateDays.Sum(x => (long)x.LateSeconds)) : null,
                earlyDays.Count, earlyDays.Count > 0 ? TimeSpan.FromSeconds(earlyDays.Sum(x => (long)x.EarlyLeaveSeconds)) : null,
                notes.Count > 0 ? string.Join("；", notes) : null);
        }
        return sheet;
    }

    private static SpreadsheetSheet Notes(AttendanceRecordsDto data, AttendanceRulesDto rules, DateTimeOffset exportedAt, AttendanceRecordsFilter filter)
    {
        var sheet = new SpreadsheetSheet("說明", [new("項目", 16), new("內容", 110)]) { Filter = false };
        sheet.Add("查詢區間", Invariant($"{data.From:yyyy/MM/dd}～{data.To:yyyy/MM/dd}（出勤日，台灣時間）"));
        sheet.Add("匯出時間", Invariant($"{exportedAt:yyyy/MM/dd HH:mm:ss}（台灣時間）")
            + (data.To >= data.Today ? Invariant($"；今天（{data.Today:yyyy/MM/dd}）尚未判定，之後的日子不列出") : ""));
        List<string> filters = [];
        if (filter.UserId is not null) filters.Add("指定人員");
        if (filter.CardNumber is not null) filters.Add($"指定卡號 {filter.CardNumber}");
        if (filter.UnitId is not null) filters.Add("指定單位（含下層）");
        if (filter.IncludeExcluded == true) filters.Add("含不列入出勤的人");
        if (filter.IncludeUnattributed == true) filters.Add("含未歸戶卡號");
        sheet.Add("條件", filters.Count > 0 ? string.Join("、", filters) + $"，共 {data.People.Count} 人" : $"資料範圍內的全部人員，共 {data.People.Count} 人");
        sheet.Add("放假日", filter.IncludeDaysOff == true ? "全部列出" : "只列有刷卡或假單的放假日；沒有刷卡也沒有假單的放假日不列，也不計入統計");
        if (!data.CalendarAvailable)
            sheet.Add(new SpreadsheetCell("假日行事曆", SpreadsheetTone.Danger), "無法取得：公文系統暫時連不上，週末當放假、平日一律無法判定，也不判遲到、早退與異常。");
        if (!data.LeaveAvailable)
            sheet.Add(new SpreadsheetCell("假單", SpreadsheetTone.Danger), "無法取得：公文系統暫時連不上，沒刷卡的日子改為無法判定。");
        sheet.Add();
        sheet.Add(new SpreadsheetCell("上下班時間", SpreadsheetTone.Strong));
        foreach (var schedule in rules.Schedules)
            sheet.Add(DayTypes[schedule.DayType], $"表定 {AttendanceRules.Time(schedule.ScheduledStart)}～{AttendanceRules.Time(schedule.ScheduledEnd)}，" +
                $"彈性上班 {AttendanceRules.Time(schedule.FlexStart)}～{AttendanceRules.Time(schedule.FlexEnd)}，應下班＝上班＋{AttendanceRules.Hours(schedule.WorkHours)} 小時");
        sheet.Add();
        sheet.Add(new SpreadsheetCell("判定規則", SpreadsheetTone.Strong));
        foreach (var rule in rules.Rules) sheet.Add(rule.Title, rule.Text);
        return sheet;
    }

    /// <summary>使用者寫帳號；沒有帳號的資料寫它依附的卡號。</summary>
    private static string? Identity(AttendancePersonDto person) => person.UserId is not null ? person.Account : person.CardNumber;

    /// <summary>刷卡與應下班的台灣時刻；判定保證它們都落在出勤日當天。</summary>
    private static TimeOnly? Clock(DateTimeOffset? value) => value is { } at ? TimeOnly.FromTimeSpan(TaipeiTime.Of(at).TimeOfDay) : null;

    /// <summary>日期格式裡的 <c>/</c> 是文化相依的分隔符號，一律以不變文化格式化。</summary>
    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    private static string Weekday(DateOnly date) => "日一二三四五六"[(int)date.DayOfWeek].ToString();

    private static string? Sheets(AttendanceRecordDto day)
    {
        if (day.Leave == LeaveState.Unavailable) return "無法取得";
        if (day.Sheets.Count == 0) return null;
        return string.Join("；", day.Sheets.Select(x =>
            Invariant($"{SheetKinds[x.Kind]}：{x.Title}（{TaipeiTime.Of(x.StartsAt):MM/dd HH:mm}–{TaipeiTime.Of(x.EndsAt):MM/dd HH:mm}，{x.FlowStatus}，{x.DesNo}）")));
    }

    // 以下字樣與前端 attendance-presentation.ts 相同；匯出在後端產生，所以各寫一份。
    private static readonly Dictionary<AttendanceStatus, string> Statuses = new()
    {
        [AttendanceStatus.Present] = "出勤", [AttendanceStatus.OnLeave] = "請假", [AttendanceStatus.Anomaly] = "異常",
        [AttendanceStatus.Undetermined] = "無法判定", [AttendanceStatus.DayOff] = "放假"
    };

    private static readonly Dictionary<AttendanceReason, string> Reasons = new()
    {
        [AttendanceReason.NoPunchOrLeave] = "沒有刷卡，也沒有假單",
        [AttendanceReason.SinglePunch] = "只有一筆刷卡，分不出上班或下班",
        [AttendanceReason.LeaveUnavailable] = "假單無法取得，沒刷卡的日子不判異常",
        [AttendanceReason.DayTypeUnknown] = "假日行事曆無法取得，不確定是否上班日",
        [AttendanceReason.DayInProgress] = "今天尚未結束，打卡檔晚上才匯入"
    };

    private static readonly Dictionary<AttendanceDayType, string> DayTypes = new()
    {
        [AttendanceDayType.FullDay] = "上班日", [AttendanceDayType.MorningOff] = "上午放假", [AttendanceDayType.AfternoonOff] = "下午放假",
        [AttendanceDayType.DayOff] = "放假", [AttendanceDayType.Unknown] = "未知"
    };

    private static readonly Dictionary<LeaveSheetKind, string> SheetKinds = new() { [LeaveSheetKind.Leave] = "假單", [LeaveSheetKind.Travel] = "出差單" };
}
