using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.CalendarSync.Management;
using AiNexus.Features.Platform.Google;
using AiNexus.Features.Platform.Google.Calendar;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>
/// Google 日曆同步：排程任務 <see cref="Sync.CalendarSyncTask"/> 定期把 GD 的假單、出差單（簽核中與已核准，回看 <c>LookbackDays</c> 天）
/// 同步成共用日曆上的事件。每段期間在 <c>calendarsync.CalendarEvents</c> 一列，推送冪等（事件 ID 由列 ID 決定），失敗依退避重試。
/// Google 的呼叫交給 Google 模組（<see cref="IGoogleCalendar"/>）；管理頁看狀態、事件與執行紀錄，可立即同步與測試連線。
/// 只在設定 <c>CalendarSync:Enabled</c> 的站台執行；開發與正式共用資料庫，列以日曆 ID 區隔，排程任務的租約保證同時只有一個程序在同步。
/// </summary>
public sealed class CalendarSyncModule : IFeatureModule
{
    /// <summary>連線測試會實際換權杖並呼叫 Google，每人每分鐘 5 次。</summary>
    public const string ConnectionTestRateLimit = "calendar-sync-connection-test";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        // 日曆 ID 只在這裡修剪一次：同步、監控與管理端點用同一個值，測試連線過了、背景同步卻因前後空白 404 的情況不會發生。
        builder.Services.AddSettings<CalendarSyncOptions, CalendarSyncOptionsValidator>(CalendarSyncOptions.Section)
            .PostConfigure(x => x.CalendarId = x.CalendarId.Trim());
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(ConnectionTestRateLimit, 5));
    }

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapGroup("/calendar-sync").RequireFeature(FeatureCatalog.CalendarSync)
        .MapSlice<GetCalendarSyncStatus>()
        .MapSlice<ListCalendarSyncCalendars>()
        .MapSlice<ListCalendarSyncEvents>()
        .MapSlice<ListCalendarSyncRuns>()
        .MapSlice<ListCalendarSyncRunItems>()
        .MapSlice<ResetCalendarSync>()
        .MapSlice<StartCalendarSyncRun>()
        .MapSlice<TestCalendarSyncConnection>();

    /// <summary>啟用同步卻讀不到服務帳戶金鑰時啟動失敗，不要等到第一輪才在背景默默失敗。</summary>
    public static void VerifyStartup(IServiceProvider services)
    {
        if (!services.GetRequiredService<IOptions<CalendarSyncOptions>>().Value.Enabled) return;
        services.GetRequiredService<GoogleServiceAccount>().Verify();
    }
}
