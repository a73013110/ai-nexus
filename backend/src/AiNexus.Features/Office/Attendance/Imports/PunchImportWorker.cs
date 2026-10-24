using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>一次掃描的結果，測試用。<paramref name="SourceAvailable"/> 為 false 表示來源資料夾讀不到，其他欄位都是 0。</summary>
public sealed record PunchScanResult(bool SourceAvailable, int Found, int Imported, int Failed, int Waiting);

/// <summary>
/// 定期掃描打卡機輸出資料夾，把需要重匯的每日檔（<see cref="PunchImportPlanner"/>）交給 <see cref="PunchImporter"/>；
/// 依修改時間與大小判斷，所以打卡機增加輸出次數不必改程式。啟動後立即掃描一次，之後每 <c>ScanIntervalSeconds</c> 一次；未啟用時不掃描。
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
        if (!options.Value.Import.Enabled) return;
        // 網路共用不通時第一次列舉可能卡很久；BackgroundService 在第一個 await 之前是同步執行，會擋住整個 host 的啟動。
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
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
    /// 掃描一次。來源資料夾讀不到不拋例外，記入 <see cref="PunchImportState"/> 並回傳 <c>SourceAvailable = false</c>；
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
            if (state.SourceFailed(ex.GetType().Name)) LogSourceUnavailable(logger, ex.GetType().Name);
            return new(false, 0, 0, 0, 0);
        }
        if (state.SourceRead()) LogSourceRestored(logger);

        var (imported, failed, waiting) = (0, 0, 0);
        // 匯入一個檔可能把別的檔標記為要重匯（見 PunchImporter 的鄰檔補救），這時開頭載入的狀態已經過時；有匯入就再掃一輪，最多三輪。
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var known = await StatesAsync(ct);
            var (passImported, passFailed) = (0, 0);
            waiting = 0;
            foreach (var info in files)
            {
                var now = clock.GetUtcNow();
                if (!PunchImportPlanner.NeedsImport(known.GetValueOrDefault(info.Name), info, now, settings)) continue;
                if (PunchImportPlanner.IsSettling(info, now, settings))
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

    private async Task<Dictionary<string, PunchFileState>> StatesAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var rows = await PunchImportPlanner.States(scope.ServiceProvider.GetRequiredService<AppDbContext>()).ToListAsync(ct);
        return rows.ToDictionary(x => x.File.FileName, StringComparer.OrdinalIgnoreCase);
    }

    [LoggerMessage(EventId = 3403, EventName = "attendance.source_unavailable", Level = LogLevel.Warning, Message = "Punch file source folder cannot be read ({ErrorType}); imports are paused until it is reachable.")]
    private static partial void LogSourceUnavailable(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3404, EventName = "attendance.source_restored", Level = LogLevel.Information, Message = "Punch file source folder is readable again.")]
    private static partial void LogSourceRestored(ILogger logger);

    [LoggerMessage(EventId = 3405, EventName = "attendance.import_aborted", Level = LogLevel.Warning, Message = "Punch file import aborted before it could be recorded ({ErrorType}); it will be retried on the next scan.")]
    private static partial void LogImportAborted(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3406, EventName = "attendance.scan_aborted", Level = LogLevel.Warning, Message = "Punch file scan aborted ({ErrorType}); it will be retried on the next interval.")]
    private static partial void LogScanAborted(ILogger logger, string errorType);
}
