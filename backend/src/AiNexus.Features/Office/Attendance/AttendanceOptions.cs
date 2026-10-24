using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance;

/// <summary>出勤設定（<c>Attendance</c> 區段）：刷卡匯入（<c>Import</c>）與出勤判定規則（<c>Workday</c>），各自一個子區段並自行驗證。</summary>
public sealed class AttendanceOptions
{
    public const string Section = "Attendance";

    [ValidateObjectMembers] public PunchImportOptions Import { get; set; } = new();

    [ValidateObjectMembers] public WorkdayOptions Workday { get; set; } = new();
}

/// <summary>
/// 出勤判定規則（ADR 0018「出勤判定」）：全公司一套，依當天的日型（GD <c>Pub_Days</c> 的 AM、PM）各一份 <see cref="WorkdayRule"/>。
/// 判定在查詢時計算、不存檔，改了就全面套用；整天放假沒有規則（不判定）。
/// </summary>
public sealed class WorkdayOptions : IValidatableObject
{
    [ValidateObjectMembers] public WorkdayRule FullDay { get; set; } = new() { ScheduledStart = new(8, 30), ScheduledEnd = new(18, 0), FlexStart = new(8, 0), FlexEnd = new(9, 0), WorkHours = 9.5 };

    [ValidateObjectMembers] public WorkdayRule MorningOff { get; set; } = new() { ScheduledStart = new(13, 30), ScheduledEnd = new(18, 0), FlexStart = new(13, 0), FlexEnd = new(14, 0), WorkHours = 4.5 };

    [ValidateObjectMembers] public WorkdayRule AfternoonOff { get; set; } = new() { ScheduledStart = new(8, 30), ScheduledEnd = new(12, 0), FlexStart = new(8, 0), FlexEnd = new(9, 0), WorkHours = 3.5 };

    /// <summary>
    /// 跨欄位規則在這裡逐一檢查而不是放在 <see cref="WorkdayRule"/>：產生的驗證器不會替巢狀物件的 <see cref="IValidatableObject"/> 結果加上路徑，
    /// 三份規則欄位同名，訊息必須指出是哪一個日型。新增日型時一併加進來。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, rule) in new[] { (nameof(FullDay), FullDay), (nameof(MorningOff), MorningOff), (nameof(AfternoonOff), AfternoonOff) })
        {
            foreach (var (message, members) in rule.Problems())
                yield return new(message, members.Select(x => $"{nameof(AttendanceOptions.Workday)}.{name}.{x}"));
        }
    }
}

/// <summary>
/// 一種日型的上下班規則，時間都是台灣本地時間。早於 <see cref="FlexStart"/> 上班以它計；晚於 <see cref="FlexEnd"/> 為遲到（從 <see cref="FlexEnd"/> 起算），
/// 應下班改為 <see cref="ScheduledEnd"/>；否則應下班＝上班＋<see cref="WorkHours"/>（含午休）。
/// </summary>
public sealed class WorkdayRule
{
    /// <summary>表定上班。判定不直接使用（遲到從彈性終點起算），只作為彈性區間的基準與畫面說明。</summary>
    public TimeOnly ScheduledStart { get; set; }

    /// <summary>遲到時的應下班時間：遲到的假單通常從表定上班請到抵達，補請後仍是表定下班。</summary>
    public TimeOnly ScheduledEnd { get; set; }

    public TimeOnly FlexStart { get; set; }

    public TimeOnly FlexEnd { get; set; }

    /// <summary>上班到應下班的時數，含午休（全天 9.5＝8 小時工時＋1.5 小時午休）。</summary>
    [Range(0.5, 16)] public double WorkHours { get; set; }

    /// <summary>跨欄位的錯誤（訊息、相關欄位），由 <see cref="WorkdayOptions"/> 加上日型路徑。</summary>
    internal IEnumerable<(string Message, string[] Members)> Problems()
    {
        if (!(FlexStart <= ScheduledStart && ScheduledStart <= FlexEnd))
            yield return ($"{nameof(FlexStart)} <= {nameof(ScheduledStart)} <= {nameof(FlexEnd)} is required.", [nameof(FlexStart), nameof(ScheduledStart), nameof(FlexEnd)]);
        if (ScheduledEnd <= ScheduledStart)
            yield return ($"{nameof(ScheduledEnd)} must be later than {nameof(ScheduledStart)}.", [nameof(ScheduledEnd)]);
        // 刷卡依刷卡時間歸到出勤日，應下班跨過午夜就落到隔天的刷卡，判定會錯；最晚的應下班是彈性終點上班＋時數。
        if (FlexEnd.ToTimeSpan() + TimeSpan.FromHours(WorkHours) >= TimeSpan.FromDays(1))
            yield return ($"{nameof(FlexEnd)} + {nameof(WorkHours)} must stay within the same day.", [nameof(FlexEnd), nameof(WorkHours)]);
    }
}

/// <summary>刷卡匯入：背景工作掃描打卡機輸出的每日檔（ADR 0018）。</summary>
public sealed class PunchImportOptions : IValidatableObject
{
    /// <summary>只控制背景掃描；HR 手動重匯不受影響，只要來源資料夾讀得到。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>打卡機輸出每日檔（<c>yyyyMMdd.txt</c>）的資料夾，通常是網路共用；IIS 應用程式集區的身分必須能讀取。</summary>
    [Required, MaxLength(260)] public string SourceDirectory { get; set; } = @"\\192.168.2.233\Data\HAMS\";

    [Range(10, 86400)] public int ScanIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// 最後修改時間離現在不到這麼久的檔案先不匯入：打卡機可能還在寫，讀到半行會讓整檔匯入失敗（失敗不影響資料，只是多一筆失敗紀錄與警示）。
    /// 0 表示不等待。
    /// </summary>
    [Range(0, 3600)] public int SettleSeconds { get; set; } = 30;

    /// <summary>
    /// 最近一次匯入失敗、檔案也沒有再變動的檔案，至少隔這麼久才由排程重試，避免壞檔每個掃描週期都多一筆失敗紀錄；檔案一有變動就立刻重匯。
    /// 0 表示每次掃描都重試。
    /// </summary>
    [Range(0, 86400)] public int FailedRetrySeconds { get; set; } = 900;

    /// <summary>超過這麼久沒有任何成功的匯入，監控判為不健康。打卡機每天輸出，預設 36 小時＝漏掉一次輸出。</summary>
    [Range(1, 720)] public int StaleAfterHours { get; set; } = 36;

    /// <summary>單一檔案大小上限。每日檔每張卡只有最早與最晚兩筆，正常遠小於此；超過視為異常，整檔匯入失敗。</summary>
    [Range(1024, 64 * 1024 * 1024)] public int MaxFileBytes { get; set; } = 8 * 1024 * 1024;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // UNC 另外判斷：非 Windows 的開發機上 Path.IsPathFullyQualified 會把 UNC 路徑當成相對路徑而讓啟動失敗。
        if (SourceDirectory.Any(char.IsControl) || !(Path.IsPathFullyQualified(SourceDirectory) || SourceDirectory.StartsWith(@"\\", StringComparison.Ordinal)))
            yield return new($"{nameof(SourceDirectory)} must be an absolute path or a UNC share.", [nameof(SourceDirectory)]);
    }
}

[OptionsValidator]
public sealed partial class AttendanceOptionsValidator : IValidateOptions<AttendanceOptions>;
