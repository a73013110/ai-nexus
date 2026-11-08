using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>
/// 資料庫裡的一個日曆：<see cref="Configured"/> 是本站設定的那個（管理頁預設看它）；
/// <see cref="Total"/> 是事件列數（含已移除），<see cref="Pending"/>、<see cref="Failed"/> 是待同步與推送失敗的列數。
/// </summary>
public sealed record CalendarSyncCalendarDto(string CalendarId, bool Configured, int Total, int Pending, int Failed);

/// <summary>
/// 開發與正式共用資料庫，各站台的事件以日曆 ID 區隔（含從舊系統轉入的列）；管理頁以這份清單切換要看的日曆。
/// 本站設定的日曆即使還沒有任何列也列出、排在最前，其餘依列數多到少。只有執行紀錄的日曆（事件都被清掉了）也列出，紀錄才查得到。
/// 日曆 ID 不分大小寫比對，與資料庫定序和事件、紀錄的篩選一致，設定的大小寫與資料不同時不會列成兩個。
/// </summary>
internal sealed class ListCalendarSyncCalendars(AppDbContext db, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/calendars", (ListCalendarSyncCalendars handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("日曆 ID 與各狀態的數量，不含個人資料").WithName("ListCalendarSyncCalendars");

    public async Task<IReadOnlyList<CalendarSyncCalendarDto>> HandleAsync(CancellationToken ct)
    {
        var configured = options.Value.CalendarId;
        var counts = await db.CalendarEvents.AsNoTracking().GroupBy(x => new { x.CalendarId, x.State })
            .Select(x => new { x.Key.CalendarId, x.Key.State, Count = x.Count() }).ToListAsync(ct);
        var withRuns = await db.SyncRuns.AsNoTracking().Select(x => x.CalendarId).Distinct().ToListAsync(ct);

        IEnumerable<string> ids = [.. counts.Select(x => x.CalendarId), .. withRuns, .. configured.Length == 0 ? [] : new[] { configured }];
        return ids.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id =>
            {
                var states = counts.Where(x => string.Equals(x.CalendarId, id, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => x.State).ToDictionary(x => x.Key, x => x.Sum(y => y.Count));
                return new CalendarSyncCalendarDto(id, string.Equals(id, configured, StringComparison.OrdinalIgnoreCase), states.Values.Sum(),
                    states.GetValueOrDefault(CalendarEventStates.Pending), states.GetValueOrDefault(CalendarEventStates.Failed));
            })
            .OrderByDescending(x => x.Configured).ThenByDescending(x => x.Total).ThenBy(x => x.CalendarId, StringComparer.Ordinal)
            .ToList();
    }
}
