using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Attendance.Calendar;
using AiNexus.Features.Office.Attendance.Leave;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄查詢結果。<paramref name="Today"/> 是查詢當下的台灣日期：晚於它的日子不回傳，當天不判成異常。
/// <paramref name="CalendarAvailable"/>、<paramref name="LeaveAvailable"/> 為 false 表示 GD 連不上：日型退回「週末放假、平日無法判定」，假單為無法取得。
/// <paramref name="Total"/> 是名單的總人數，<paramref name="People"/> 是從 <paramref name="Offset"/> 起的這一頁。
/// </summary>
public sealed record AttendanceRecordsDto(DateOnly From, DateOnly To, DateOnly Today, bool CalendarAvailable, bool LeaveAvailable, int Total, int Offset, IReadOnlyList<AttendancePersonDto> People);

/// <summary>
/// 出勤名單上的一列。<paramref name="UserId"/> 為 null 是沒有帳號的資料，依附在 <paramref name="CardNumber"/>：
/// 只有姓名的持有人（<paramref name="DisplayName"/> 是持有期間的姓名），或 <paramref name="Unattributed"/> 的未歸戶卡號（只列有刷卡的日子）。
/// <paramref name="LeaveAvailable"/> 為 false：GD 連不上、這個人對不到公文帳號或沒有帳號，他的假單一律是無法取得；
/// <paramref name="Excluded"/> 是 HR 標為不列入出勤（只有明確要求或指定時才會出現）。<paramref name="Days"/> 只含持有員工卡（未歸戶卡號是有刷卡）的日子。
/// </summary>
public sealed record AttendancePersonDto(Guid? UserId, string? CardNumber, string DisplayName, string? Account, bool LeaveAvailable, bool Excluded, bool Unattributed,
    IReadOnlyList<AttendanceRecordDto> Days);

/// <summary>
/// 一個出勤日：<see cref="AttendanceEvaluation"/> 的判定結果加上當天有交集的假單與出差單（原始起訖，只並列不比對時數）。
/// <paramref name="CalendarRemark"/> 是假日設定的備註（例如「國慶日」），沒有時為 null。
/// </summary>
public sealed record AttendanceRecordDto(
    DateOnly Date,
    AttendanceDayType DayType,
    string? CalendarRemark,
    AttendanceStatus Status,
    AttendanceReason? Reason,
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    DateTimeOffset? ExpectedClockOut,
    int LateSeconds,
    int EarlyLeaveSeconds,
    LeaveState Leave,
    IReadOnlyList<LeaveSheet> Sheets);

/// <summary>
/// 出勤紀錄（ADR 0018）：<c>from</c>～<c>to</c>（出勤日，都含，最多 <see cref="AttendanceRecordsFilter.MaxRangeDays"/> 天）內，範圍看得到的人每天的判定結果。
/// 名單是 <see cref="AttendanceRoster"/>：期間內持有員工卡的人（全公司範圍可含未歸戶卡號），可再以 <c>userId</c>、<c>card</c>、<c>unitId</c> 縮小，與資料範圍取交集。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>依人分頁（<c>offset</c>、<c>limit</c>）：區間最長一年，全公司整年是數萬列，畫面一次只畫一頁的人；匯出以 <see cref="Page.All"/> 不分頁。</item>
/// <item>查詢次數固定：持有期間、刷卡（依持有期間與日在資料庫彙總最早、最晚）、行事曆與假單各查一次，只查這一頁的人，不逐人逐日查詢。</item>
/// <item>放假日沒有刷卡也沒有假單時預設不列（<c>includeDaysOff</c> 才列）；有刷卡或假單的放假日一律列出，加班或出差不會被藏起來。</item>
/// <item>GD 連不上不拒絕整個請求：行事曆與假單各自降級，刷卡照常顯示（ADR 0018）。</item>
/// </list>
/// </remarks>
internal sealed class ListAttendanceRecords(AppDbContext db, IDataScope scopes, AttendanceRoster roster, CalendarReader calendar,
    LeaveSheetReader leave, IOptions<AttendanceOptions> options, TimeProvider clock)
{
    /// <summary>每頁人數的預設與上限：一人一年約 250 個上班日，50 人已是上萬列。</summary>
    public const int DefaultPageSize = 20, MaxPageSize = 50;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", ([AsParameters] AttendanceRecordsFilter filter, int? offset, int? limit, ICurrentUser user, ListAttendanceRecords handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, filter, new(offset ?? 0, limit ?? DefaultPageSize), ct).ToHttpResultAsync())
        .WithName("ListAttendanceRecords");

    /// <summary>名單的一頁；<see cref="Limit"/> 為 null 是整份名單（匯出）。</summary>
    public readonly record struct Page(int Offset, int? Limit)
    {
        public static Page All => new(0, null);
    }

    public async Task<Result<AttendanceRecordsDto>> HandleAsync(Guid viewer, AttendanceRecordsFilter filter, Page page, CancellationToken ct)
    {
        if (filter.Range() is not { } range) return AttendanceErrors.RecordsRangeInvalid;
        if (!filter.IsValid() || page.Offset < 0 || page.Limit is < 1 or > MaxPageSize) return AttendanceErrors.RecordsFilterInvalid;

        var (start, end) = range;
        var today = TaipeiTime.Today(clock);
        // 未來的日子沒有刷卡也沒有結論，不回傳；名單也只看到今天為止，下週才開始持卡的人不出現。
        var last = end < today ? end : today;
        var scope = await scopes.ForAsync(viewer, FeatureCatalog.AttendanceRecords, ct);
        if (scope.IsEmpty || last < start) return new AttendanceRecordsDto(start, end, today, true, true, 0, page.Offset, []);

        var source = await roster.SourceAsync(viewer, scope, filter, (start, last), ct);
        var everyone = await roster.PeopleAsync(source, ct);
        IReadOnlyList<RosterPerson> people = page.Limit is { } limit ? [.. everyone.Skip(page.Offset).Take(limit)] : everyone;
        if (people.Count == 0) return new AttendanceRecordsDto(start, end, today, true, true, everyone.Count, page.Offset, []);

        var punches = await PunchesAsync(source, people, people.Count == everyone.Count, start, last, ct);
        var days = await calendar.ReadAsync(start, last, ct);
        var calendarDays = days?.ToDictionary(x => x.Date);
        var sheets = await leave.ReadAsync([.. people.Where(x => x.UserId is not null).Select(x => x.UserId!.Value)], start, last, ct);
        var rules = options.Value.Workday;
        var includeDaysOff = filter.IncludeDaysOff == true;

        var result = people.Select((person, index) =>
        {
            // 還沒有帳號的人對不到公文帳號，假單與 GD 連不上時一樣是無法取得。
            var leaveAvailable = person.UserId is { } userId && sheets.Available && !sheets.Unmapped.Contains(userId);
            var filedSheets = person.UserId is { } id ? sheets.For(id) : [];
            var records = new List<AttendanceRecordDto>();
            for (var date = start; date <= last; date = date.AddDays(1))
            {
                var (dayStart, dayEnd) = (TaipeiTime.At(date, TimeOnly.MinValue), TaipeiTime.At(date.AddDays(1), TimeOnly.MinValue));
                // 只回傳持有員工卡的日子：到職前、換卡空窗或離職後沒有卡，判成「沒刷卡」是誤報。未歸戶卡號沒有持有人，只列有刷卡的日子。
                if (person.Unattributed ? !punches.ContainsKey((index, date)) : !person.Holdings.Any(p => p.StartsAt < dayEnd && (p.EndsAt is null || p.EndsAt > dayStart)))
                    continue;
                // 單據是原始起訖，與這一天有交集就算當天有假單或出差單（跨日、半天都一樣），時數由 HR 判斷。
                IReadOnlyList<LeaveSheet> filed = [.. filedSheets.Where(s => s.StartsAt < dayEnd && s.EndsAt > dayStart)];
                DateTimeOffset? clockIn = null, clockOut = null;
                if (punches.TryGetValue((index, date), out var punch)) (clockIn, clockOut) = punch;
                var calendarDay = calendarDays?.GetValueOrDefault(date);
                var dayType = AttendanceEvaluator.DayTypeOf(date, calendarDay);
                if (!includeDaysOff && dayType == AttendanceDayType.DayOff && clockIn is null && filed.Count == 0) continue;
                var state = !leaveAvailable ? LeaveState.Unavailable : filed.Count > 0 ? LeaveState.Filed : LeaveState.None;
                var e = AttendanceEvaluator.Evaluate(new(date, dayType, clockIn, clockOut, state), rules, today);
                records.Add(new(e.Date, e.DayType, Remark(calendarDay), e.Status, e.Reason,
                    e.ClockIn, e.ClockOut, e.ExpectedClockOut, e.LateSeconds, e.EarlyLeaveSeconds, e.Leave, filed));
            }
            return new AttendancePersonDto(person.UserId, person.CardNumber, person.DisplayName, person.Account, leaveAvailable, person.Excluded, person.Unattributed, records);
        }).ToList();
        return new AttendanceRecordsDto(start, end, today, days is not null, sheets.Available, everyone.Count, page.Offset, result);
    }

    /// <summary>
    /// 每人每天最早與最晚的刷卡，鍵是人在 <paramref name="people"/> 裡的位置。資料庫依持有期間（未歸戶卡號依卡號）與日彙總，同一人同時持有多張卡時再合併。
    /// 整份名單（<paramref name="everyone"/>）以子查詢比對，不把持有期間展開成參數；分頁時只查這一頁的人。
    /// view 在同一張卡的期間重疊時會重複列，最早與最晚不受影響。
    /// </summary>
    private async Task<Dictionary<(int Person, DateOnly Date), (DateTimeOffset First, DateTimeOffset Last)>> PunchesAsync(
        RosterSource source, IReadOnlyList<RosterPerson> people, bool everyone, DateOnly start, DateOnly last, CancellationToken ct)
    {
        var held = db.Set<AttributedPunch>().AsNoTracking().Where(x => x.AttendanceDate >= start && x.AttendanceDate <= last);
        if (everyone)
        {
            var ids = source.Holdings.Select(x => (Guid?)x.Id);
            held = held.Where(x => ids.Contains(x.AssignmentId));
        }
        else
        {
            List<Guid?> ids = [.. people.SelectMany(x => x.Holdings).Select(x => (Guid?)x.Id)];
            held = held.Where(x => ids.Contains(x.AssignmentId));
        }
        var rows = await held
            .GroupBy(x => new { x.AssignmentId, x.AttendanceDate })
            .Select(g => new { g.Key.AssignmentId, g.Key.AttendanceDate, First = g.Min(x => x.PunchedAt), Last = g.Max(x => x.PunchedAt) })
            .ToListAsync(ct);

        var owner = people.SelectMany((person, index) => person.Holdings.Select(x => (x.Id, index))).ToDictionary(x => x.Id, x => x.index);
        var result = new Dictionary<(int, DateOnly), (DateTimeOffset, DateTimeOffset)>();
        void Add(int person, DateOnly date, DateTimeOffset first, DateTimeOffset latest)
        {
            var key = (person, date);
            result[key] = result.TryGetValue(key, out var seen) ? (Min(seen.Item1, first), Max(seen.Item2, latest)) : (first, latest);
        }
        foreach (var row in rows)
            if (row.AssignmentId is { } id && owner.TryGetValue(id, out var person)) Add(person, row.AttendanceDate, row.First, row.Last);

        // 卡號比對與資料庫定序一致（不分大小寫），對回名單時也不分大小寫。
        var cardOwner = people.Select((person, index) => (person, index)).Where(x => x.person.Unattributed)
            .ToDictionary(x => x.person.CardNumber!, x => x.index, StringComparer.OrdinalIgnoreCase);
        if (source.Unattributed is null || cardOwner.Count == 0) return result;
        var unattributed = source.Unattributed;
        if (!everyone)
        {
            List<string> numbers = [.. cardOwner.Keys];
            unattributed = unattributed.Where(x => numbers.Contains(x.CardNumber));
        }
        var cards = await unattributed
            .GroupBy(x => new { x.CardNumber, x.AttendanceDate })
            .Select(g => new { g.Key.CardNumber, g.Key.AttendanceDate, First = g.Min(x => x.PunchedAt), Last = g.Max(x => x.PunchedAt) })
            .ToListAsync(ct);
        foreach (var row in cards)
            if (cardOwner.TryGetValue(row.CardNumber, out var person)) Add(person, row.AttendanceDate, row.First, row.Last);
        return result;
    }

    private static string? Remark(CalendarDayDto? day) => string.IsNullOrWhiteSpace(day?.Remark) ? null : day.Remark.Trim();

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
}
