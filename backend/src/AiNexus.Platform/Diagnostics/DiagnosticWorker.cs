using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷日誌的背景服務。檔案日誌（Journal）與 SQL 匯入是兩條獨立的軌道；
/// 即使 SQL 緩慢或離線，也不會拖慢或阻塞本機檔案的即時寫入。
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

            // 匯入器因容量上限被阻塞時，清理仍必須能執行。
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
            // 主機關閉期限已逾。不可啟動並行的寫入器，也不能假裝記憶體佇列已排清；未結的容量控制碼由日誌生命週期負責關閉。
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
            // 剩餘的佇列會記在下方的緊急狀態；絕不可透過 ILogger 寫入，否則會遞迴。
        }

        var remaining = pending.Count + Interlocked.Read(ref health.QueueDepth);
        if (remaining > 0) health.Emergency("shutdown_flush_incomplete", remaining);
    }
}
