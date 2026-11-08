using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>
/// 公文假單、出差單同步到 Google 日曆的設定（<c>CalendarSync</c> 區段）。服務帳戶在 <c>Google</c> 區段，GD 連線在 <c>Databases:Gd</c>。
/// 開發與正式共用資料庫時，只在正式站打開 <see cref="Enabled"/>；開發機要試時改指測試日曆（不同日曆各自一組列，不會動到正式日曆的事件）。
/// </summary>
public sealed class CalendarSyncOptions : IValidatableObject
{
    public const string Section = "CalendarSync";

    /// <summary>是否執行背景同步與手動同步；關閉時管理頁仍可查看紀錄與測試連線。間隔是排程任務 <c>calendar-sync.sync</c> 的設定（<c>Scheduling:Tasks</c>）。</summary>
    public bool Enabled { get; set; }

    /// <summary>目標 Google 日曆的 ID（日曆設定的「整合日曆」，例如 <c>xxxx@group.calendar.google.com</c>）；日曆要分享給服務帳戶並給「變更活動」權限。</summary>
    [MaxLength(200)] public string CalendarId { get; set; } = "";

    /// <summary>
    /// 比對的期間窗：結束時間在這麼多天以前到未來的表單。窗外已同步的事件不再比對、也不刪除，
    /// 所以表單在很久以後才被銷單或刪除時，日曆上的舊事件會留著；GD 查詢與比對量也因此不會隨歷史變大。
    /// </summary>
    [Range(1, 365)] public int LookbackDays { get; set; } = 30;

    /// <summary>同時對 Google 送出的請求數；Calendar API 對單一使用者有每秒速率限制，不要開太大。</summary>
    [Range(1, 10)] public int MaxParallelism { get; set; } = 4;

    /// <summary>一輪最多推送幾筆；其餘留到下一輪，避免第一次同步或大量變動時一輪跑太久。</summary>
    [Range(10, 5000)] public int MaxPushesPerRun { get; set; } = 500;

    /// <summary>
    /// 安全閥：一輪要移除的事件超過窗內現有事件的這個比例（且至少 <see cref="RemovalHoldMinimum"/> 筆）時，整批不移除，等管理員確認。
    /// GD 回傳不完整（例如被截斷、表單暫時被搬走）時，不會把日曆清空。
    /// </summary>
    [Range(0.05, 1.0)] public double RemovalHoldRatio { get; set; } = 0.3;

    /// <summary>移除筆數少於這個值時不觸發安全閥，日常的退件、銷單不必確認。</summary>
    [Range(1, 1000)] public int RemovalHoldMinimum { get; set; } = 10;

    /// <summary>推送失敗後的重試間隔上限；從 1 分鐘起每次加倍，到這個上限為止。</summary>
    [Range(5, 1440)] public int MaxRetryDelayMinutes { get; set; } = 360;

    /// <summary>重設日曆時被 Google 限速，至少暫停多久再繼續刪；Google 用 <c>Retry-After</c> 要求更久時聽它的。</summary>
    [Range(1, 300)] public int RateLimitPauseSeconds { get; set; } = 20;

    /// <summary>執行紀錄（<c>calendarsync.SyncRuns</c>）的保存天數。</summary>
    [Range(7, 3650)] public int RunRetentionDays { get; set; } = 180;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled && string.IsNullOrWhiteSpace(CalendarId))
            yield return new("CalendarId is required when calendar sync is enabled.", [nameof(CalendarId)]);
    }
}

[OptionsValidator]
public sealed partial class CalendarSyncOptionsValidator : IValidateOptions<CalendarSyncOptions>;
