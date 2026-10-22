using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Staff.Attendance;

/// <summary>出勤設定（<c>Attendance</c> 區段）；之後的出勤判定規則（ADR 0018）也放在這個區段，各自一個子區段並自行驗證。</summary>
public sealed class AttendanceOptions
{
    public const string Section = "Attendance";

    [ValidateObjectMembers] public PunchImportOptions Import { get; set; } = new();
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
