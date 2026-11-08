using AiNexus.Data.App;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Platform.Google;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>
/// 管理頁頂端的狀態：本站的設定（是否啟用、日曆、間隔、期間窗）與服務帳戶信箱，讓管理員知道日曆要分享給誰；
/// <see cref="Running"/> 含已排隊的手動同步，<see cref="LastCheck"/> 是排程任務最近一輪的結果（沒有異動的排程輪不寫同步紀錄，只在這裡看得到）。
/// 各日曆的數量見 <see cref="ListCalendarSyncCalendars"/>。
/// </summary>
public sealed record CalendarSyncStatusDto(
    bool Enabled,
    string? CalendarId,
    int IntervalSeconds,
    int LookbackDays,
    bool ServiceAccountConfigured,
    string? ServiceAccount,
    bool Running,
    CalendarSyncCheck? LastCheck);

internal sealed class GetCalendarSyncStatus(ScheduledTasks tasks, AppDbContext db, GoogleServiceAccount account, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/status", (GetCalendarSyncStatus handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("同步設定與最近一輪的狀態，不含個人資料").WithName("GetCalendarSyncStatus");

    public async Task<CalendarSyncStatusDto> HandleAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var progress = await CalendarSyncChecks.ReadAsync(tasks, db, settings.CalendarId, ct);
        return new CalendarSyncStatusDto(settings.Enabled, settings.CalendarId.Length == 0 ? null : settings.CalendarId, (int)progress.Interval.TotalSeconds,
            settings.LookbackDays, account.Configured, account.ClientEmail, progress.Running, progress.LastCheck);
    }
}
