using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Office.Attendance.Calendar;
using AiNexus.Platform.Monitoring;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>
/// 刷卡匯入的監控。未啟用 idle；來源資料夾讀不到 failing；有檔案最近一次匯入失敗，或上次成功匯入之後有一個工作日整天過去了 degraded。
/// HAMS 沒有新刷卡就不寫檔，週末與連假本來就沒有匯入，所以不能只看隔了幾小時；工作日一定有人刷卡，整天沒有匯入代表 HAMS 排程或來源資料夾出了問題，
/// HR 看到的出勤資料已經過期。工作日依 GD 行事曆（整天放假以外都是），GD 讀不到或沒有那天時以週一到週五計。
/// 只在上次成功匯入早於昨天時才查 GD（平日不查），監控又只在有人檢視時讀取，不必快取。
/// 來源讀不到沒有檔案可記，看排程任務最近一輪的結果，所以不論哪個程序掃描都看得到。
/// 失敗的檔案走 <c>ix_PunchFiles_LastImportId</c>，最近成功的匯入走 <c>ix_PunchImports_StartedAt</c>。
/// 出勤的每個外部來源各自一個監控來源，放在擁有它的能力資料夾（同 Inference 的 <c>generation</c> 與 <c>model-providers</c>）。
/// </summary>
internal sealed class PunchImportMonitoring(AppDbContext db, ScheduledTasks tasks, CalendarReader calendar, IOptions<AttendanceOptions> options, TimeProvider clock) : IMonitoringSource
{
    /// <summary>連假不會超過這麼多天；更久沒有匯入不必查行事曆就是過期，也不會為了很舊的紀錄查一大段 GD。</summary>
    private const int LongestHoliday = 31;

    public string Id => "attendance-import";
    public string Name => "出勤刷卡匯入";
    public int Order => 38;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var settings = options.Value.Import;
        var now = clock.GetUtcNow();
        var imports = db.PunchImports.AsNoTracking();
        var failedFiles = await (from file in db.PunchFiles.AsNoTracking()
                                 join import in imports on file.LastImportId equals import.Id
                                 where import.Status == PunchImportStatuses.Failed
                                 select file.Id).CountAsync(ct);
        var lastSuccess = await imports.Where(x => x.Status == PunchImportStatuses.Succeeded).OrderByDescending(x => x.StartedAt)
            .Select(x => (DateTimeOffset?)x.FinishedAt).FirstOrDefaultAsync(ct);
        var day = now.AddDays(-1);
        var importedDay = await imports.CountAsync(x => x.Status == PunchImportStatuses.Succeeded && x.StartedAt >= day, ct);
        var lastScan = (await tasks.GetStateAsync(PunchImportTask.Definition.Key, ct))?.LastRun;
        var sourceUnreadable = lastScan is { Status: ScheduledTaskRunStatuses.Failed } && lastScan.ErrorCode == AttendanceErrors.PunchSourceUnavailable.Code;
        double? sinceSuccess = lastSuccess is { } finished ? Math.Max(0, Math.Round((now - finished).TotalSeconds)) : null;
        // 前面的狀態已經不健康時不必再查 GD。
        var missed = settings.Enabled && !sourceUnreadable && failedFiles == 0 && lastSuccess is { } success ? await MissedWorkdayAsync(success, ct) : null;

        var (status, summary) =
            !settings.Enabled ? (SubsystemStates.Idle, "刷卡匯入未啟用，不會自動掃描打卡檔。")
            : sourceUnreadable ? (SubsystemStates.Failing, "讀不到打卡檔來源資料夾，匯入已暫停。請確認網路共用與 IIS 集區身分的讀取權限。")
            : failedFiles > 0 ? (SubsystemStates.Degraded, $"{failedFiles} 個打卡檔最近一次匯入失敗，出勤資料可能不是最新。詳見匯入紀錄。")
            : missed is { } missedDay ? (SubsystemStates.Degraded, $"{missedDay:yyyy-MM-dd} 是工作日卻沒有成功匯入，請確認 HAMS 的接收與轉出排程。")
            : lastSuccess is null ? (SubsystemStates.Idle, "尚未匯入任何打卡檔。")
            : (SubsystemStates.Healthy, "打卡檔匯入正常。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("failed_files", "匯入失敗的檔案", failedFiles, failedFiles > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_success", "距上次成功匯入", sinceSuccess, missed is not null ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("imported_24h", "24 小時內成功匯入", importedDay),
            SubsystemFact.Words("source", "來源資料夾", sourceUnreadable ? "讀不到" : "可讀取", sourceUnreadable ? SubsystemTones.Danger : SubsystemTones.Neutral),
        ];
        return new(status, summary, facts);
    }

    /// <summary>上次成功匯入的隔天到昨天（台灣日期）之間的第一個工作日；沒有為 null。今天的檔中午才轉出，不算在內。</summary>
    private async Task<DateOnly?> MissedWorkdayAsync(DateTimeOffset lastSuccess, CancellationToken ct)
    {
        var from = DateOnly.FromDateTime(lastSuccess.ToTaipei().DateTime).AddDays(1);
        var to = TaipeiTime.Today(clock).AddDays(-1);
        if (from > to) return null;
        if (to.DayNumber - from.DayNumber >= LongestHoliday) return from;
        var days = await calendar.ReadAsync(from, to, ct);
        var dayOff = days?.ToDictionary(x => x.Date, x => x.MorningOff && x.AfternoonOff) ?? [];
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var off = dayOff.TryGetValue(day, out var known) ? known : day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (!off) return day;
        }
        return null;
    }
}
