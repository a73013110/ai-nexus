using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷日誌背景服務。檔案系統日誌寫入（Journal）與 SQL 資料庫匯入雙軌獨立運作，
/// 確保即使 SQL 資料庫緩慢或離線，亦絕不拖慢或阻塞即時日誌寫入本機檔案系統。
/// </summary>
public sealed class DiagnosticWorker(DiagnosticBuffer buffer, IDiagnosticJournal journal, IDiagnosticStore store, DiagnosticHealth health, IOptions<DiagnosticOptions> options, DiagnosticExporter exporter, TimeProvider clock) : BackgroundService
{
    private List<DiagnosticEvent> pending = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var writer = WriteLoopAsync(stoppingToken);
        var importer = ImportLoopAsync(stoppingToken);
        try
        {
            await Task.WhenAll(writer, importer);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (pending.Count == 0) pending = buffer.Drain();
            if (pending.Count > 0)
            {
                try
                {
                    await journal.AppendAsync(pending, ct);
                    health.Recovered("journal");
                    exporter.Export(pending);
                    pending = [];
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    var reason = e is DiagnosticCapacityException ? "journal_capacity_exhausted"
                        : e is UnauthorizedAccessException or System.Security.SecurityException ? "journal_access_denied"
                        : (e.HResult & 0xffff) is 112 or 39 ? "journal_disk_full"
                        : "journal_write_failed";
                    health.Emergency(reason);
                    await Task.Delay(options.Value.RetrySeconds * 1000, ct);
                }
            }
            else
            {
                await Task.Delay(options.Value.FlushIntervalMs, ct);
            }
        }
    }

    private async Task ImportLoopAsync(CancellationToken ct)
    {
        var cleanupAt = DateTimeOffset.MinValue;
        while (!ct.IsCancellationRequested)
        {
            var delay = options.Value.FlushIntervalMs;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.SqlTimeoutSeconds * 4));
                await journal.ReplayAsync(store, timeout.Token);
                if (health.PendingBytes == 0) health.Recovered("sql");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                var reason = e is DiagnosticCapacityException ? "sql_capacity_exhausted"
                    : e is Microsoft.Data.SqlClient.SqlException sql ? "sql_import_failed_" + sql.Number
                    : "sql_import_failed";
                health.Emergency(reason);
                delay = options.Value.RetrySeconds * 1000;
            }

            // 清理機制必須在匯入器達到容量上限遭阻塞時仍持續運作
            if (!ct.IsCancellationRequested && cleanupAt < clock.GetUtcNow())
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.SqlTimeoutSeconds * 4));
                    await store.CleanupAsync(timeout.Token);
                    journal.Cleanup();
                    health.Recovered("cleanup");
                    cleanupAt = clock.GetUtcNow().AddMinutes(5);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    health.Emergency("sql_cleanup_failed");
                    cleanupAt = clock.GetUtcNow().AddSeconds(options.Value.RetrySeconds);
                }
            }
            await Task.Delay(delay, ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.StopAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        if (ExecuteTask is { IsCompleted: false })
        {
            // 主機關閉期限逾期。切勿啟動並行寫入器或偽稱記憶體佇列已完全衝刷。日誌生命週期會負責關閉未結的容量控制碼。
            health.Emergency("shutdown_flush_incomplete", pending.Count + Interlocked.Read(ref health.QueueDepth));
            return;
        }

        using var flush = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        flush.CancelAfter(TimeSpan.FromSeconds(options.Value.ShutdownSeconds));
        try
        {
            while (!flush.IsCancellationRequested)
            {
                if (pending.Count == 0) pending = buffer.Drain();
                if (pending.Count == 0) break;
                await journal.AppendAsync(pending, flush.Token);
                exporter.Export(pending);
                pending = [];
            }
        }
        catch (Exception)
        {
            // 剩餘佇列記錄於下方緊急狀態；絕不可透過 ILogger 產生遞迴
        }

        var remaining = pending.Count + Interlocked.Read(ref health.QueueDepth);
        if (remaining > 0) health.Emergency("shutdown_flush_incomplete", remaining);
    }
}
