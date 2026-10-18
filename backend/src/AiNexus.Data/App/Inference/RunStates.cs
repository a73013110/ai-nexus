namespace AiNexus.Data.App.Inference;

/// <summary>
/// 生成任務狀態常數定義與有效性輔助工具。
/// </summary>
public static class RunStates
{
    /// <summary>排隊中：已持久化至資料庫並在記憶體 Channel 排隊等待 Worker 接手。</summary>
    public const string Queued = "queued";

    /// <summary>生成中：Worker 已取得租約並正在向模型供應商發起串流請求。</summary>
    public const string Running = "running";

    /// <summary>已完成：生成完整結束，回答內容已寫入資料庫。</summary>
    public const string Completed = "completed";

    /// <summary>已取消：使用者主動中止生成或超時取消。</summary>
    public const string Cancelled = "cancelled";

    /// <summary>失敗：生成過程中遭遇未處理例外或供應商錯誤。</summary>
    public const string Failed = "failed";

    /// <summary>判斷此狀態是否屬於仍在進行中（Queued 或 Running）的活躍任務。</summary>
    public static bool IsActive(string status) => status is Queued or Running;
}
