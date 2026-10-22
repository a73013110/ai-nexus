using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>一次掃描的結果，測試與日後的監控用。<paramref name="SourceAvailable"/> 為 false 表示來源資料夾讀不到，其他欄位都是 0。</summary>
public sealed record PunchScanResult(bool SourceAvailable, int Found, int Imported, int Failed, int Waiting);

/// <summary>
/// 定期掃描打卡機輸出資料夾，把有變動的每日檔交給 <see cref="PunchImporter"/>（單一 worker，同一時間只匯入一個檔）。
/// 要不要重匯看 <see cref="PunchImportPlanner"/>：修改時間或大小與上次成功匯入的不同、最近一次失敗、或被標記為要重匯。
/// 所以打卡機增加輸出次數不必改程式。啟動後立即掃描一次，之後每 <c>ScanIntervalSeconds</c> 一次。
/// </summary>
public sealed partial class PunchImportWorker(
    IServiceScopeFactory scopes,
    IOptions<AttendanceOptions> options,
    PunchImportState state,
    ILogger<PunchImportWorker> logger,
    TimeProvider clock) : BackgroundService
{
    private const int MaxPasses = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 網路共用不通時第一次列舉可能卡很久；BackgroundService 在第一個 await 之前是同步執行，會擋住整個 host 的啟動。
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.Import.Enabled) await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogScanAborted(logger, ex.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.Import.ScanIntervalSeconds), clock, stoppingToken);
        }
    }

    /// <summary>
    /// 掃描一次。來源資料夾讀不到不拋例外，記入 <see cref="PunchImportState"/> 與日誌並回傳 <c>SourceAvailable = false</c>；
    /// 個別檔案匯入時的非預期錯誤（例如資料庫連不上）只影響該檔，繼續處理其他檔。檔名日期由舊到新處理，所以重複的刷卡歸先寫入的檔。
    /// </summary>
    public async Task<PunchScanResult> ScanAsync(CancellationToken ct)
    {
        var settings = options.Value.Import;
        List<FileInfo> files;
        try
        {
            files = await Task.Run(() => List(settings.SourceDirectory), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            state.Failed(clock.GetUtcNow(), ex.GetType().Name);
            LogSourceUnavailable(logger, ex.GetType().Name);
            return new(false, 0, 0, 0, 0);
        }
        if (state.Succeeded(clock.GetUtcNow())) LogSourceRestored(logger);

        var (imported, failed, waiting) = (0, 0, 0);
        var retryAfter = TimeSpan.FromSeconds(settings.FailedRetrySeconds);
        var settle = TimeSpan.FromSeconds(settings.SettleSeconds);
        // 匯入一個檔可能把別的檔標記為要重匯（見 PunchImporter 的鄰檔補救），這時開頭載入的狀態已經過時；有匯入就再掃一輪，最多三輪。
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var known = await KnownAsync(ct);
            var (passImported, passFailed) = (0, 0);
            waiting = 0;
            foreach (var info in files)
            {
                var now = clock.GetUtcNow();
                var modifiedAt = PunchImporter.ModifiedAt(info);
                known.TryGetValue(info.Name, out var current);
                if (!PunchImportPlanner.NeedsImport(current?.File, current?.Last, modifiedAt, info.Length, now, retryAfter)) continue;
                if (PunchImportPlanner.IsSettling(modifiedAt, now, settle))
                {
                    waiting++;
                    continue;
                }
                try
                {
                    using var scope = scopes.CreateScope();
                    var import = await scope.ServiceProvider.GetRequiredService<PunchImporter>().ImportIfChangedAsync(info.Name, ct);
                    if (import is null) continue;
                    if (import.Status == PunchImportStatuses.Succeeded) passImported++;
                    else passFailed++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 連失敗紀錄都寫不進去（資料庫不可用）：沒有匯入列，下一輪掃描會再試。
                    passFailed++;
                    LogImportAborted(logger, ex.GetType().Name);
                }
            }
            imported += passImported;
            failed += passFailed;
            if (passImported == 0) break;
        }
        return new(true, files.Count, imported, failed, waiting);
    }

    /// <summary>每日檔（<c>yyyyMMdd.txt</c>），檔名日期由舊到新；其他檔案不匯入。屬性由列舉一次取得，不再逐檔 stat。</summary>
    private static List<FileInfo> List(string directory) => [.. new DirectoryInfo(directory).EnumerateFiles()
        .Where(x => PunchFileParser.TryParseFileName(x.Name, out _)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];

    private async Task<Dictionary<string, Known>> KnownAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await (from file in db.Set<PunchFile>().AsNoTracking()
                          join import in db.Set<PunchImport>().AsNoTracking() on file.LastImportId equals import.Id into imports
                          from last in imports.DefaultIfEmpty()
                          select new Known(file, last)).ToListAsync(ct);
        return rows.ToDictionary(x => x.File.FileName, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Known(PunchFile File, PunchImport? Last);

    [LoggerMessage(EventId = 3403, EventName = "attendance.source_unavailable", Level = LogLevel.Warning, Message = "Punch file source folder cannot be read ({ErrorType}); imports are paused until it is reachable.")]
    private static partial void LogSourceUnavailable(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3404, EventName = "attendance.source_restored", Level = LogLevel.Information, Message = "Punch file source folder is readable again.")]
    private static partial void LogSourceRestored(ILogger logger);

    [LoggerMessage(EventId = 3405, EventName = "attendance.import_aborted", Level = LogLevel.Warning, Message = "Punch file import aborted before it could be recorded ({ErrorType}); it will be retried on the next scan.")]
    private static partial void LogImportAborted(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3406, EventName = "attendance.scan_aborted", Level = LogLevel.Warning, Message = "Punch file scan aborted ({ErrorType}); it will be retried on the next interval.")]
    private static partial void LogScanAborted(ILogger logger, string errorType);
}
