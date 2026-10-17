namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷事件資料庫持久化儲存介面。
/// 支援將批次日誌寫入關聯式資料庫及定期清理逾期與超量歷史紀錄。
/// </summary>
public interface IDiagnosticStore
{
    /// <summary>
    /// 將已脫敏之診斷事件批次非同步寫入資料庫。
    /// </summary>
    Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct);

    /// <summary>
    /// 依據保留原則非同步清除逾期或超出最大列數配額的舊日誌。
    /// </summary>
    Task CleanupAsync(CancellationToken ct);
}
