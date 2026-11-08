using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>排程任務端點與執行的預期失敗。</summary>
public static class SchedulingErrors
{
    public static readonly Error TaskNotFound = Error.NotFound("scheduled_task_not_found");

    /// <summary>任務被設定停用，或它宣告的條件不成立（例如功能沒有啟用），不能手動執行。</summary>
    public static readonly Error TaskUnavailable = Error.Conflict("scheduled_task_unavailable");

    /// <summary>一輪超過宣告的逾時，已取消。</summary>
    public static readonly Error TaskTimedOut = Error.Timeout("scheduled_task_timed_out");

    /// <summary>任務拋出非外部服務的例外；只寫日誌與執行紀錄。</summary>
    public static readonly Error TaskFailed = Error.Unavailable("scheduled_task_failed");
}
