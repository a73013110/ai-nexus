namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷事件的資料庫持久化介面，只負責批次寫入；保存期清理是排程任務（Admin/Diagnostics 的 <c>DiagnosticRetentionTask</c>）。
/// </summary>
public interface IDiagnosticStore
{
    /// <summary>把已脫敏的診斷事件批次非同步寫入資料庫。</summary>
    Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct);
}
