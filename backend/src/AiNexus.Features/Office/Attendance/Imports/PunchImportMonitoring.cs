using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>
/// 刷卡匯入的監控。未啟用 idle；來源資料夾讀不到 failing；有檔案最近一次匯入失敗，或超過 <c>StaleAfterHours</c> 沒有任何成功匯入 degraded。
/// 打卡機每天輸出一個新檔，所以太久沒有成功匯入代表打卡機或網路共用出了問題，HR 看到的出勤資料已經過期。
/// 失敗的檔案走 <c>ix_PunchFiles_LastImportId</c>，最近成功的匯入走 <c>ix_PunchImports_StartedAt</c>。
/// 出勤的每個外部來源各自一個監控來源，放在擁有它的能力資料夾（同 Inference 的 <c>generation</c> 與 <c>model-providers</c>）。
/// </summary>
internal sealed class PunchImportMonitoring(AppDbContext db, PunchImportState state, IOptions<AttendanceOptions> options, TimeProvider clock) : IMonitoringSource
{
    public string Id => "attendance-import";
    public string Name => "出勤刷卡匯入";
    public int Order => 38;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var settings = options.Value.Import;
        var now = clock.GetUtcNow();
        var imports = db.Set<PunchImport>().AsNoTracking();
        var failedFiles = await (from file in db.Set<PunchFile>().AsNoTracking()
                                 join import in imports on file.LastImportId equals import.Id
                                 where import.Status == PunchImportStatuses.Failed
                                 select file.Id).CountAsync(ct);
        var lastSuccess = await imports.Where(x => x.Status == PunchImportStatuses.Succeeded).OrderByDescending(x => x.StartedAt)
            .Select(x => (DateTimeOffset?)x.FinishedAt).FirstOrDefaultAsync(ct);
        var day = now.AddDays(-1);
        var importedDay = await imports.CountAsync(x => x.Status == PunchImportStatuses.Succeeded && x.StartedAt >= day, ct);
        var sourceError = state.SourceError;
        double? sinceSuccess = lastSuccess is { } finished ? Math.Max(0, Math.Round((now - finished).TotalSeconds)) : null;
        var stale = sinceSuccess > TimeSpan.FromHours(settings.StaleAfterHours).TotalSeconds;

        var (status, summary) =
            !settings.Enabled ? (SubsystemStates.Idle, "刷卡匯入未啟用，不會自動掃描打卡檔。")
            : sourceError is not null ? (SubsystemStates.Failing, "讀不到打卡檔來源資料夾，匯入已暫停；請確認網路共用與 IIS 集區身分的讀取權限。")
            : failedFiles > 0 ? (SubsystemStates.Degraded, $"{failedFiles} 個打卡檔最近一次匯入失敗，出勤資料可能不是最新；詳見匯入紀錄。")
            : stale ? (SubsystemStates.Degraded, $"超過 {settings.StaleAfterHours} 小時沒有成功匯入，請確認打卡機是否有輸出檔案。")
            : lastSuccess is null ? (SubsystemStates.Idle, "尚未匯入任何打卡檔。")
            : (SubsystemStates.Healthy, "打卡檔匯入正常。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("failed_files", "匯入失敗的檔案", failedFiles, failedFiles > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_success", "距上次成功匯入", sinceSuccess, stale ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("imported_24h", "24 小時內成功匯入", importedDay),
            SubsystemFact.Words("source", "來源資料夾", sourceError is null ? "可讀取" : "讀不到", sourceError is null ? SubsystemTones.Neutral : SubsystemTones.Danger),
        ];
        return new(status, summary, facts);
    }
}
