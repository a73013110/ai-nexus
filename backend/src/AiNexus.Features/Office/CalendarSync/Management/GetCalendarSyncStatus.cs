using AiNexus.Features.Platform.Google;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>
/// 管理頁頂端的狀態：本站的設定（是否啟用、日曆、間隔、期間窗）與服務帳戶信箱，讓管理員知道日曆要分享給誰；
/// <see cref="LastCheck"/> 是記憶體中的最近一輪（沒有異動的排程輪不寫紀錄，只在這裡看得到），重啟後為 null。
/// 各日曆的數量見 <see cref="ListCalendarSyncCalendars"/>。只讀設定與記憶體，輪詢不碰資料庫。
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

internal sealed class GetCalendarSyncStatus(CalendarSyncState state, GoogleServiceAccount account, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/status", (GetCalendarSyncStatus handler) => handler.Handle())
        .NotAudited("同步設定與最近一輪的狀態，不含個人資料").WithName("GetCalendarSyncStatus");

    public CalendarSyncStatusDto Handle()
    {
        var settings = options.Value;
        return new CalendarSyncStatusDto(settings.Enabled, settings.CalendarId.Length == 0 ? null : settings.CalendarId, settings.IntervalSeconds,
            settings.LookbackDays, account.Configured, account.ClientEmail, state.Running, state.LastCheck);
    }
}
