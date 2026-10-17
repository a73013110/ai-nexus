using AiNexus.Platform.Errors;

namespace AiNexus.Features.Chat;

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
}
