namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷事件的資料庫持久化介面，負責批次寫入，並定期清除逾期與超量的歷史紀錄。
/// </summary>
public interface IDiagnosticStore
{
    /// <summary>把已脫敏的診斷事件批次非同步寫入資料庫。</summary>
    Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct);

    /// <summary>依保留政策，非同步清除逾期或超出最大列數的舊紀錄。</summary>
    Task CleanupAsync(CancellationToken ct);
}
