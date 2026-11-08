using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Office.CalendarSync.Management;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>同步與管理端點共用的查詢：依日曆篩選、比對範圍、執行紀錄投影成 DTO。</summary>
internal static class CalendarSyncQueries
{
    /// <summary>只看一個日曆；空的表示全部。</summary>
    public static IQueryable<CalendarEvent> OfCalendar(this IQueryable<CalendarEvent> rows, string? calendarId)
        => string.IsNullOrWhiteSpace(calendarId) ? rows : rows.Where(x => x.CalendarId == calendarId.Trim());

    /// <inheritdoc cref="OfCalendar(IQueryable{CalendarEvent}, string?)"/>
    public static IQueryable<SyncRun> OfCalendar(this IQueryable<SyncRun> runs, string? calendarId)
        => string.IsNullOrWhiteSpace(calendarId) ? runs : runs.Where(x => x.CalendarId == calendarId.Trim());

    /// <summary>同步還在管的事件：沒有移除、結束時間在期間窗內。比對與事件列表共用，條件對應 <c>ix_CalendarEvents_CalendarId_EndsAt</c>。</summary>
    public static IQueryable<CalendarEvent> InSyncWindow(this IQueryable<CalendarEvent> rows, DateTimeOffset windowStart)
        => rows.Where(x => x.State != CalendarEventStates.Removed && x.EndsAt >= windowStart);

    public static IQueryable<CalendarSyncRunDto> ToDtos(this IQueryable<SyncRun> runs, AppDbContext db)
        => from r in runs
           join u in db.Users on r.RequestedById equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new CalendarSyncRunDto(r.Id, r.CalendarId, r.TriggerKind, r.RequestedById, u.DisplayName, r.StartedAt, r.FinishedAt, r.Status, r.SourceCount,
               r.CreatedCount, r.UpdatedCount, r.DeletedCount, r.FailedCount, r.HeldCount, r.ErrorCode, r.ErrorMessage, r.LookbackDays);
}
