using System.ComponentModel.DataAnnotations;
using Cronos;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>
/// 排程引擎的設定（<c>Scheduling</c> 區段）。開發機與正式站共用資料庫時，開發機要關掉 <see cref="Execute"/>，
/// 否則兩邊都會搶著跑同一批任務（租約保證不重疊，但開發機的版本可能不同）。
/// </summary>
public sealed class SchedulingOptions : IValidatableObject
{
    public const string Section = "Scheduling";

    /// <summary>這個程序是否執行排程任務；關閉時管理頁照樣可看、可要求執行，由其他程序執行。</summary>
    public bool Execute { get; set; } = true;

    /// <summary>同時執行的任務上限；慢的任務（例如同步外部系統）不會擋住其他任務。</summary>
    [Range(1, 16)] public int MaxConcurrency { get; set; } = 4;

    /// <summary>查詢到期任務的間隔；同一程序內的手動要求會立即喚醒，不必等這個間隔。</summary>
    [Range(1, 60)] public int PollSeconds { get; set; } = 5;

    /// <summary>沒事可做（idle）的執行紀錄保存天數。</summary>
    [Range(1, 30)] public int IdleRunRetentionDays { get; set; } = 2;

    /// <summary>其他執行紀錄的保存天數。</summary>
    [Range(7, 3650)] public int RunRetentionDays { get; set; } = 90;

    /// <summary>個別任務的覆寫，鍵是任務 Key（例如 <c>mail.retention</c>）；鍵必須是已註冊的任務，否則啟動失敗。</summary>
    public Dictionary<string, ScheduledTaskSettings> Tasks { get; set; } = new(StringComparer.Ordinal);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (key, settings) in Tasks)
        {
            var path = $"{nameof(Tasks)}:{key}";
            if (settings.Interval is not null && settings.Cron is not null)
                yield return new($"{path}: set either Interval or Cron, not both.", [path]);
            if (settings.Interval is { } interval && (interval < TimeSpan.FromSeconds(10) || interval > TimeSpan.FromDays(31)))
                yield return new($"{path}:Interval must be between 00:00:10 and 31.00:00:00.", [path]);
            if (settings.Timeout is { } timeout && (timeout < TimeSpan.FromSeconds(10) || timeout > TimeSpan.FromDays(1)))
                yield return new($"{path}:Timeout must be between 00:00:10 and 1.00:00:00.", [path]);
            if (settings.Cron is { } cron && !CronExpression.TryParse(cron, out _))
                yield return new($"{path}:Cron '{cron}' is not a valid five-field cron expression.", [path]);
        }
    }
}

/// <summary>單一任務的覆寫；省略的欄位沿用程式宣告的值。</summary>
public sealed class ScheduledTaskSettings
{
    /// <summary>false 時不執行（依排程與手動都不行），管理頁顯示為設定停用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>改成固定間隔，例如 <c>00:10:00</c>。</summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>改成 cron（台灣時間），例如 <c>0 3 * * *</c>。</summary>
    public string? Cron { get; set; }

    public TimeSpan? Timeout { get; set; }
}

[OptionsValidator]
public sealed partial class SchedulingOptionsValidator : IValidateOptions<SchedulingOptions>;
