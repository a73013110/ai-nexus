using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Platform.Google;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>
/// 管理頁頂端的狀態。設定（是否啟用、日曆、間隔、期間窗）與服務帳戶信箱讓管理員知道日曆要分享給誰；
/// <see cref="LastCheck"/> 是記憶體中的最近一輪（沒有異動的排程輪不寫紀錄，只在這裡看得到），重啟後為 null；
/// <see cref="LastRun"/> 是最近一筆執行紀錄。數量只算目前設定的日曆。
/// </summary>
public sealed record CalendarSyncStatusDto(
    bool Enabled,
    string? CalendarId,
    int IntervalSeconds,
    int LookbackDays,
    bool ServiceAccountConfigured,
    string? ServiceAccount,
    bool Running,
    CalendarSyncCheck? LastCheck,
    CalendarSyncRunDto? LastRun,
    int Pending,
    int Failed,
    int Synced,
    int Removed);

internal sealed class GetCalendarSyncStatus(AppDbContext db, CalendarSyncState state, GoogleServiceAccount account, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/status", (GetCalendarSyncStatus handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("同步狀態與數量，不含個人資料").WithName("GetCalendarSyncStatus");

    public async Task<Result<CalendarSyncStatusDto>> HandleAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var calendarId = settings.CalendarId;
        var counts = calendarId.Length == 0 ? [] : await db.CalendarEvents.AsNoTracking().Where(x => x.CalendarId == calendarId)
            .GroupBy(x => x.State).Select(x => new { State = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.State, x => x.Count, ct);
        var lastRun = calendarId.Length == 0 ? null : await db.SyncRuns.AsNoTracking().Where(x => x.CalendarId == calendarId)
            .OrderByDescending(x => x.StartedAt).ToDtos(db).FirstOrDefaultAsync(ct);
        return new CalendarSyncStatusDto(settings.Enabled, calendarId.Length == 0 ? null : calendarId, settings.IntervalSeconds, settings.LookbackDays,
            account.Configured, account.ClientEmail, state.Running, state.LastCheck, lastRun,
            counts.GetValueOrDefault(CalendarEventStates.Pending), counts.GetValueOrDefault(CalendarEventStates.Failed),
            counts.GetValueOrDefault(CalendarEventStates.Synced), counts.GetValueOrDefault(CalendarEventStates.Removed));
    }
}
