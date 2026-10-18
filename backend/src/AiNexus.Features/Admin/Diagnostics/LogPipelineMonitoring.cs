using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>
/// 日誌管線的完整監控事實，來源是記憶體中的 <see cref="DiagnosticHealth"/>；日誌頁只標示是否降級，細節在這裡。
/// 計數自程序啟動起累計；磁碟位元組與 SQL 筆數只是最新一次讀值。
/// </summary>
internal sealed class LogPipelineMonitoring(DiagnosticHealth health, TimeProvider clock) : IMonitoringSource
{
    public string Id => "log-pipeline";
    public string Name => "日誌管線";
    public int Order => 40;
    public TimeSpan Refresh => TimeSpan.FromSeconds(5);

    public Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var state = health.Snapshot();
        var last = new[] { state.LastSqlWrite, state.LastFileWrite }.Max();
        double? Age(DateTimeOffset? at) => at is { } value ? Math.Max(0, Math.Round((clock.GetUtcNow() - value).TotalSeconds)) : null;
        var age = Age(last);
        var degraded = state.Degraded;
        var summary = !degraded ? "日誌正常寫入。"
            : state.Failure is not null ? $"日誌寫入異常（{state.Failure}），已遺失 {state.Lost} 筆。"
            : state.Lost > 0 ? $"啟動後已遺失 {state.Lost} 筆日誌。"
            : state.Corrupt > 0 ? $"啟動後有 {state.Corrupt} 筆日誌損毀。"
            : "日誌有待補寫的資料。";
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("queue_depth", "待寫入", state.QueueDepth),
            SubsystemFact.Count("lost", "遺失", state.Lost, state.Lost > 0 ? SubsystemTones.Danger : SubsystemTones.Neutral),
            SubsystemFact.Count("corrupt", "損毀", state.Corrupt, state.Corrupt > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("write_failures", "寫入失敗", state.WriteFailures, state.WriteFailures > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Bytes("pending_bytes", "待補寫", state.PendingBytes, state.PendingBytes > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("replayed", "已補寫", state.Replayed),
            SubsystemFact.Count("sampled", "採樣略過", state.Sampled),
            SubsystemFact.Bytes("disk_bytes", "暫存大小", state.DiskBytes),
            SubsystemFact.Count("estimated_sql_rows", "SQL 筆數（估計）", state.EstimatedSqlRows),
            SubsystemFact.Count("export_failures", "選配匯出失敗", state.ExportFailures, state.ExportFailures > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("last_write", "上次寫入", age),
            SubsystemFact.Seconds("last_sql_write", "SQL 上次寫入", Age(state.LastSqlWrite)),
            SubsystemFact.Seconds("last_file_write", "檔案上次寫入", Age(state.LastFileWrite)),
        ];
        return Task.FromResult(new SubsystemReading(degraded ? SubsystemStates.Degraded : SubsystemStates.Healthy, summary, facts));
    }
}
