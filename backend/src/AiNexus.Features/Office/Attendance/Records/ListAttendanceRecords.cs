using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Office.Attendance.Calendar;
using AiNexus.Features.Office.Attendance.Leave;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄查詢結果。<paramref name="Today"/> 是查詢當下的台灣日期：晚於它的日子不回傳，當天不判成異常。
/// <paramref name="CalendarAvailable"/>、<paramref name="LeaveAvailable"/> 為 false 表示 GD 連不上：日型退回「週末放假、平日無法判定」，假單為無法取得。
/// </summary>
public sealed record AttendanceRecordsDto(DateOnly From, DateOnly To, DateOnly Today, bool CalendarAvailable, bool LeaveAvailable, IReadOnlyList<AttendancePersonDto> People);

/// <summary>
/// 出勤名單上的一個人。<paramref name="LeaveAvailable"/> 為 false：GD 連不上或這個人對不到公文帳號，他的假單一律是無法取得；
/// <paramref name="Excluded"/> 是 HR 標為不列入出勤（只有明確要求或指定這個人時才會出現）。<paramref name="Days"/> 只含他持有員工卡的日子。
/// </summary>
public sealed record AttendancePersonDto(Guid UserId, string DisplayName, string Account, bool LeaveAvailable, bool Excluded, IReadOnlyList<AttendanceRecordDto> Days);

/// <summary>一個出勤日：<see cref="AttendanceEvaluation"/> 的判定結果加上當天有交集的假單與出差單（原始起訖，只並列不比對時數）。</summary>
public sealed record AttendanceRecordDto(
    DateOnly Date,
    AttendanceDayType DayType,
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
/// 出勤紀錄（ADR 0018）：<c>from</c>～<c>to</c>（出勤日，都含，最多 <see cref="MaxRangeDays"/> 天）內，範圍看得到的人每天的判定結果。
/// 名單是期間內持有員工卡的使用者；可再以 <c>userId</c>、<c>unitId</c>（含下層單位的目前成員）縮小，與資料範圍取交集。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>不分頁：一次最多一個月，名單是公司在職持卡人（數百人），畫面要整月逐人顯示；資料量是人數 × 天數的小列，GD 也是整批查一次。人數大到回應過大時再依人分頁。</item>
/// <item>查詢次數固定：持有期間、刷卡（依人與日在資料庫彙總最早、最晚）、行事曆與假單各查一次，不逐人逐日查詢。</item>
/// <item>GD 連不上不拒絕整個請求：行事曆與假單各自降級，刷卡照常顯示（ADR 0018）。</item>
/// </list>
/// </remarks>
internal sealed class ListAttendanceRecords(AppDbContext db, IDataScope scopes, OrganizationDirectory organization, CalendarReader calendar,
    LeaveSheetReader leave, IOptions<AttendanceOptions> options, TimeProvider clock)
{
    /// <summary>一個月：畫面一次看一個月；也限制 GD 假單查詢與回應的大小。</summary>
    public const int MaxRangeDays = 31;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (DateOnly? from, DateOnly? to, Guid? userId, Guid? unitId, bool? includeExcluded, ICurrentUser user, ListAttendanceRecords handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, from, to, userId, unitId, includeExcluded ?? false, ct).ToHttpResultAsync())
        .WithName("ListAttendanceRecords");

    public async Task<Result<AttendanceRecordsDto>> HandleAsync(Guid viewer, DateOnly? from, DateOnly? to, Guid? userId, Guid? unitId, bool includeExcluded, CancellationToken ct)
    {
        if (from is not { } start || to is not { } end || end < start || !CalendarDates.IsSupported(start) || !CalendarDates.IsSupported(end)
            || end.DayNumber - start.DayNumber >= MaxRangeDays)
            return AttendanceErrors.RecordsRangeInvalid;

        var today = TaipeiTime.Today(clock);
        // 未來的日子沒有刷卡也沒有結論，不回傳；名單也只看到今天為止，下週才開始持卡的人不出現。
        var last = end < today ? end : today;
        var scope = await scopes.ForAsync(viewer, FeatureCatalog.AttendanceRecords, ct);
        if (scope.IsEmpty || last < start) return new AttendanceRecordsDto(start, end, today, true, true, []);

        var unitIds = unitId is { } unit ? await organization.SubtreeAsync([unit], ct) : null;
        var holdings = HoldingsQuery(viewer, scope, start, last, userId, unitIds,
            // 「不列入出勤」只是 HR 看全公司時隱藏不重要的人，不是隱私控制（ADR 0018）：只在範圍是 all 時套用；
            // 本人（own）與主管（unit、unit-tree）照常看得到。指定了某個人，或明確要求含不列入的人時也不隱藏，否則 HR 查不到他。
            hideExcluded: scope.All && userId is null && !includeExcluded);
        var rows = await (from a in holdings
                          join u in db.Users on a.UserId equals u.Id
                          select new { UserId = u.Id, u.DisplayName, u.Account, a.StartsAt, a.EndsAt, Excluded = db.Set<RosterExclusion>().Any(x => x.UserId == u.Id) })
            .ToListAsync(ct);
        var people = rows.Select(x => x.UserId).Distinct().ToList();

        // 刷卡與名單同一個子查詢，不把人數展開成參數。view 在持有期間重疊時會重複列，最早與最晚不受影響。
        var holders = holdings.Select(x => x.UserId);
        var punches = (await db.Set<AttributedPunch>().AsNoTracking()
                .Where(x => x.UserId != null && x.AttendanceDate >= start && x.AttendanceDate <= last && holders.Contains(x.UserId))
                .GroupBy(x => new { x.UserId, x.AttendanceDate })
                .Select(g => new { g.Key.UserId, g.Key.AttendanceDate, First = g.Min(x => x.PunchedAt), Last = g.Max(x => x.PunchedAt) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.UserId!.Value, x.AttendanceDate), x => (x.First, x.Last));

        var days = await calendar.ReadAsync(start, last, ct);
        var calendarDays = days?.ToDictionary(x => x.Date);
        var sheets = await leave.ReadAsync(people, start, last, ct);
        var rules = options.Value.Workday;

        var result = rows.GroupBy(x => x.UserId)
            .Select(person =>
            {
                var first = person.First();
                var leaveAvailable = sheets.Available && !sheets.Unmapped.Contains(person.Key);
                var periods = person.Select(x => (x.StartsAt, x.EndsAt)).ToList();
                var records = new List<AttendanceRecordDto>();
                for (var date = start; date <= last; date = date.AddDays(1))
                {
                    var (dayStart, dayEnd) = (TaipeiTime.At(date, TimeOnly.MinValue), TaipeiTime.At(date.AddDays(1), TimeOnly.MinValue));
                    // 只回傳持有員工卡的日子：到職前、換卡空窗或離職後沒有卡，判成「沒刷卡」是誤報。
                    if (!periods.Any(p => p.StartsAt < dayEnd && (p.EndsAt is null || p.EndsAt > dayStart))) continue;
                    // 單據是原始起訖，與這一天有交集就算當天有假單或出差單（跨日、半天都一樣），時數由 HR 判斷。
                    IReadOnlyList<LeaveSheet> filed = [.. sheets.For(person.Key).Where(s => s.StartsAt < dayEnd && s.EndsAt > dayStart)];
                    var state = !leaveAvailable ? LeaveState.Unavailable : filed.Count > 0 ? LeaveState.Filed : LeaveState.None;
                    DateTimeOffset? clockIn = null, clockOut = null;
                    if (punches.TryGetValue((person.Key, date), out var punch)) (clockIn, clockOut) = (punch.First, punch.Last);
                    var dayType = AttendanceEvaluator.DayTypeOf(date, calendarDays?.GetValueOrDefault(date));
                    var e = AttendanceEvaluator.Evaluate(new(date, dayType, clockIn, clockOut, state), rules, today);
                    records.Add(new(e.Date, e.DayType, e.Status, e.Reason, e.ClockIn, e.ClockOut, e.ExpectedClockOut, e.LateSeconds, e.EarlyLeaveSeconds, e.Leave, filed));
                }
                return new AttendancePersonDto(person.Key, first.DisplayName, first.Account, leaveAvailable, first.Excluded, records);
            })
            .OrderBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Account, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId)
            .ToList();
        return new AttendanceRecordsDto(start, end, today, days is not null, sheets.Available, result);
    }

    /// <summary>
    /// 期間內（<paramref name="start"/> 00:00 到 <paramref name="last"/> 隔天 00:00，台灣時間）有交集的員工卡持有期間，已套用資料範圍與篩選。
    /// 只有姓名、還沒有帳號的持有人不在名單：沒有使用者就沒有範圍可判斷，由 HR 在卡號對應綁上帳號。
    /// </summary>
    private IQueryable<CardAssignment> HoldingsQuery(Guid viewer, EffectiveDataScope scope, DateOnly start, DateOnly last, Guid? userId, IReadOnlySet<Guid>? unitIds, bool hideExcluded)
    {
        var (periodStart, periodEnd) = (TaipeiTime.At(start, TimeOnly.MinValue), TaipeiTime.At(last.AddDays(1), TimeOnly.MinValue));
        var holdings = from a in db.Set<CardAssignment>()
                       join c in db.Set<Card>() on a.CardId equals c.Id
                       where c.Kind == CardKinds.Employee && a.UserId != null && a.StartsAt < periodEnd && (a.EndsAt == null || a.EndsAt > periodStart)
                       select a;
        if (!scope.All)
        {
            // 範圍與活動稽核相同：own＝自己，unit／unit-tree＝範圍單位「目前」的成員（組織不存歷史，人調走後由新單位的主管看到）。
            var own = scope.Own;
            var members = organization.MemberIds(scope.UnitIds);
            holdings = holdings.Where(a => own && a.UserId == viewer || members.Contains(a.UserId!.Value));
        }
        if (userId is { } person) holdings = holdings.Where(a => a.UserId == person);
        if (unitIds is not null)
        {
            var inUnit = organization.MemberIds(unitIds);
            holdings = holdings.Where(a => inUnit.Contains(a.UserId!.Value));
        }
        if (hideExcluded) holdings = holdings.Where(a => !db.Set<RosterExclusion>().Any(x => x.UserId == a.UserId));
        return holdings;
    }
}
