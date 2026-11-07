using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Google;
using AiNexus.Features.Platform.Google.Calendar;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary>
/// 連線測試的結果。失敗時 <see cref="ErrorCode"/> 用與同步相同的 <see cref="CalendarSyncFailures"/> 代碼，前端共用一份說明；
/// <see cref="Message"/> 是 Google 回應的原文（只回給發起的人，不寫入紀錄）。成功時帶日曆名稱與時區，讓管理員確認接到的是哪本日曆。
/// </summary>
public sealed record CalendarSyncConnectionDto(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    string? ServiceAccount,
    string? CalendarSummary,
    string? CalendarTimeZone,
    long ElapsedMs);

/// <summary>
/// 以目前設定實際呼叫一次 Google（換權杖＋讀日曆），不寫入日曆，也不需要先啟用同步：上線前先在正式站測過再打開。
/// 每人每分鐘 5 次，避免反覆換權杖吃掉 Google 的配額。
/// </summary>
internal sealed class TestCalendarSyncConnection(IGoogleCalendar calendar, GoogleServiceAccount account, AuditTrail audit, IOptions<CalendarSyncOptions> options, TimeProvider clock)
    : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/connection-test", (TestCalendarSyncConnection handler, CancellationToken ct) => handler.HandleAsync(ct))
        .RequireFeature(FeatureCatalog.CalendarSyncManage)
        .RequireRateLimiting(CalendarSyncModule.ConnectionTestRateLimit)
        .Audited(CalendarSyncAudit.ConnectionTested)
        .WithName("TestCalendarSyncConnection");

    public async Task<Result<CalendarSyncConnectionDto>> HandleAsync(CancellationToken ct)
    {
        var calendarId = options.Value.CalendarId;
        if (calendarId.Length == 0) return CalendarSyncErrors.CalendarNotConfigured;

        var started = clock.GetTimestamp();
        var response = await calendar.GetCalendarAsync(calendarId, ct);
        var elapsed = (long)clock.GetElapsedTime(started).TotalMilliseconds;
        var errorCode = response.IsOk ? null : CalendarSyncFailures.Of(response.Status);

        // 測試本身跑完就是成功的操作；連不連得上記在明細。
        audit.Describe(details: new { status = errorCode ?? "ok" });
        return new CalendarSyncConnectionDto(response.IsOk, errorCode, response.Message, account.ClientEmail,
            response.Value?.Summary, response.Value?.TimeZone, elapsed);
    }
}
