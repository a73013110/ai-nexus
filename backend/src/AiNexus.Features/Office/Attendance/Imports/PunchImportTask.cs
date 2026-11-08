using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>一次掃描的結果，測試用。<paramref name="SourceAvailable"/> 為 false 表示來源資料夾讀不到，其他欄位都是 0。</summary>
public sealed record PunchScanResult(bool SourceAvailable, int Found, int Imported, int Failed, int Waiting);

/// <summary>
/// 定期掃描打卡機輸出資料夾，把需要重匯的每日檔（<see cref="PunchImportPlanner"/>）交給 <see cref="PunchImporter"/>；
/// 依修改時間與大小判斷，所以 HAMS 增加轉出次數不必改程式。啟動後即掃描一次；<c>Attendance:Import:Enabled</c> 為 false 時不掃描。
/// </summary>
public sealed partial class PunchImportTask(
    IServiceScopeFactory scopes,
    IOptions<AttendanceOptions> options,
    ILogger<PunchImportTask> logger,
    TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "attendance.punch-import", "打卡檔匯入", "出勤", "掃描打卡機輸出的每日檔，匯入新的或有變動的檔。", Schedule.Every(TimeSpan.FromMinutes(5)))
    {
        Unavailable = services => services.GetRequiredService<IOptions<AttendanceOptions>>().Value.Import.Enabled ? null : "Attendance:Import:Enabled 未啟用",
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var scan = await ScanAsync(context, ct);
        if (!scan.SourceAvailable) return AttendanceErrors.PunchSourceUnavailable;
        var summary = $"{scan.Found:N0} 個檔，匯入 {scan.Imported:N0}、失敗 {scan.Failed:N0}、等待 {scan.Waiting:N0}";
        return scan.Imported + scan.Failed == 0 ? TaskOutcome.Idle(scan.Waiting > 0 ? summary : null) : TaskOutcome.Done(summary);
    }

    /// <summary>
    /// 掃描一次。來源資料夾讀不到不拋例外，回傳 <c>SourceAvailable = false</c>，由 <see cref="RunAsync"/> 記為失敗：這種失敗沒有檔案可記，
    /// 只留在排程的執行紀錄（監控讀它，所以不論哪個程序掃描都看得到），連續失敗由排程通知；
    /// 個別檔案匯入時的非預期錯誤（例如資料庫連不上）只影響該檔，繼續處理其他檔。檔名日期由舊到新處理，所以重複的刷卡歸先寫入的檔。
    /// </summary>
    public Task<PunchScanResult> ScanAsync(CancellationToken ct) => ScanAsync(null, ct);

    private async Task<PunchScanResult> ScanAsync(ScheduledTaskContext? context, CancellationToken ct)
    {
        var settings = options.Value.Import;
        List<FileInfo> files;
        try
        {
            files = await Task.Run(() => List(settings.SourceDirectory), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(false, 0, 0, 0, 0);
        }

        // 手動重匯標記要重匯的檔（PunchImporter 的鄰檔補救）在下一次掃描處理；排程匯入只新增、不會再標記別的檔，所以一輪就夠。
        var known = await StatesAsync(ct);
        var (imported, failed, waiting, checkedFiles) = (0, 0, 0, 0);
        foreach (var info in files)
        {
            context?.Report(info.Name, checkedFiles++, files.Count);
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
                if (import.Status == PunchImportStatuses.Succeeded) imported++;
                else failed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 連失敗紀錄都寫不進去（資料庫不可用）：沒有匯入列，下一輪掃描會再試。
                failed++;
                LogImportAborted(logger, ex.GetType().Name);
            }
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

    [LoggerMessage(EventId = 3405, EventName = "attendance.import_aborted", Level = LogLevel.Warning, Message = "Punch file import aborted before it could be recorded ({ErrorType}); it will be retried on the next scan.")]
    private static partial void LogImportAborted(ILogger logger, string errorType);
}
