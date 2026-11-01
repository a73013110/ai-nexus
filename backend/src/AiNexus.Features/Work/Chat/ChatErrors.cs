using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Chat;

/// <summary>
/// 對話與生成操作專用錯誤定義目錄。
/// </summary>
internal static class ChatErrors
{
    /// <summary>生成排程器尚未就緒或處於暫停/維護狀態。</summary>
    public static readonly Error SchedulerUnavailable = Error.Unavailable("scheduler_unavailable");

    /// <summary>請求所引用的父訊息歷史不合法或已被刪除，無法分支出新對話回合。</summary>
    public static readonly Error InvalidHistory = Error.Conflict("invalid_history");

    /// <summary>提問引用的附件檔案不存在或已過期刪除。</summary>
    public static readonly Error AttachmentMissing = Error.Conflict("attachment_not_found");

    /// <summary>使用者對同一生成的 SSE 串流連線數已達上限（防多頁面重複連線耗盡連線池）。</summary>
    public static readonly Error SubscriptionLimit = Error.RateLimited("subscription_limit");

    /// <summary>SSE 重播請求的序號游標無效（負數或格式錯誤）。</summary>
    public static readonly Error InvalidCursor = Error.Invalid("invalid_cursor");

    /// <summary>SSE 游標序號超出伺服器目前已產生的最大序號（客戶端游標超前）。</summary>
    public static readonly Error CursorAhead = Error.Conflict("invalid_cursor");

    // 生成任務的失敗代碼，寫在 run 的 ErrorCode；串流中途的外部失敗另見 InferenceErrors。
    public static readonly Error OutputLimitExceeded = Error.Upstream("output_limit_exceeded");
    public static readonly Error ExecutorLost = Error.Unavailable("executor_lost");
    public static readonly Error GenerationFailed = Error.Unavailable("generation_failed");
    public static readonly Error ServerStopping = Error.Unavailable("server_stopping");
    public static readonly Error GenerationTimeout = Error.Unavailable("generation_timeout");
    public static readonly Error InternalError = Error.Unavailable("internal_error");
    /// <summary>生成結束後寫回資料庫失敗；只寫日誌，run 本身記成 <see cref="InternalError"/>。</summary>
    public static readonly Error PersistenceFailed = Error.Unavailable("generation_persistence_failed");
}
