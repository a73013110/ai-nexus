using AiNexus.Features.Platform.Google;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>日曆同步端點的預期失敗。</summary>
public static class CalendarSyncErrors
{
    /// <summary>設定沒有打開同步（或沒有日曆 ID）時不能手動同步；只在正式站打開，避免開發機改到正式日曆。</summary>
    public static readonly Error Disabled = Error.Conflict("calendar_sync_disabled");

    /// <summary>還沒設定日曆 ID，連線測試沒有對象。</summary>
    public static readonly Error CalendarNotConfigured = Error.Unprocessable("calendar_sync_calendar_not_configured");
}

/// <summary>
/// 事件推送與整輪失敗的原因代碼，存在 <c>CalendarEvents.LastErrorCode</c>、<c>SyncRuns.ErrorCode</c>，前端依它顯示說明。
/// 不是 HTTP 錯誤：同步在背景執行，失敗記在資料上，不回給任何請求。名稱寫入後不可改。
/// </summary>
public static class CalendarSyncFailures
{
    public const string GoogleNotConfigured = "google_not_configured";
    public const string GoogleCredentialsRejected = "google_credentials_rejected";
    public const string GoogleForbidden = "google_forbidden";
    public const string GoogleCalendarNotFound = "google_calendar_not_found";
    public const string GoogleRateLimited = "google_rate_limited";
    public const string GoogleUnavailable = "google_unavailable";
    public const string GoogleRejected = "google_rejected";
    public const string GdUnavailable = "gd_unavailable";
    public const string RemovalsHeld = "removals_held";
    public const string Unexpected = "unexpected";

    /// <summary>Google 呼叫失敗的原因代碼。事件層級的 404／409／410 由推送流程接手或重建，走到這裡的 404 是日曆本身找不到（或沒有分享給服務帳戶）。</summary>
    public static string Of(GoogleStatus status) => status switch
    {
        GoogleStatus.NotConfigured => GoogleNotConfigured,
        GoogleStatus.CredentialRejected => GoogleCredentialsRejected,
        GoogleStatus.Forbidden => GoogleForbidden,
        GoogleStatus.NotFound => GoogleCalendarNotFound,
        GoogleStatus.RateLimited => GoogleRateLimited,
        GoogleStatus.Invalid or GoogleStatus.Conflict or GoogleStatus.Gone => GoogleRejected,
        _ => GoogleUnavailable
    };
}
