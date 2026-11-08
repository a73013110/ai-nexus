using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>
/// 排程引擎的設定（<c>Scheduling</c> 區段）。開發機與正式站共用資料庫，<see cref="Execute"/> 預設關閉：
/// 只有正式站在 appsettings.Production.json 打開，開發機打開會用自己的版本跑共用資料庫上的任務。
/// </summary>
public sealed class SchedulingOptions : IValidatableObject
{
    public const string Section = "Scheduling";

    /// <summary>這個程序是否執行排程任務；關閉時管理頁照樣可看、可要求執行，由其他程序執行。</summary>
    public bool Execute { get; set; }

    /// <summary>
    /// 非空時這個程序只執行列出的任務 Key（例如開發機只試 <c>calendar-sync.sync</c>），其他任務留給別的程序；
    /// 仍要 <see cref="Execute"/> 打開才生效。Key 必須是已註冊的任務，否則啟動失敗。
    /// </summary>
    public List<string> ExecuteOnly { get; set; } = [];

    /// <summary>同時執行的任務上限；慢的任務（例如同步外部系統）不會擋住其他任務。</summary>
    [Range(1, 16)] public int MaxConcurrency { get; set; } = 4;

    /// <summary>查詢到期任務的間隔；同一程序內的手動要求會立即喚醒，不必等這個間隔。</summary>
    [Range(1, 60)] public int PollSeconds { get; set; } = 5;

    /// <summary>沒事可做（idle）的執行紀錄保存天數。</summary>
    [Range(1, 30)] public int IdleRunRetentionDays { get; set; } = 2;

    /// <summary>其他執行紀錄的保存天數。</summary>
    [Range(7, 3650)] public int RunRetentionDays { get; set; } = 90;

    /// <summary>
    /// 每個任務的排程、逾時與開關，鍵是任務 Key（例如 <c>mail.retention</c>）。這裡是唯一來源：appsettings.json 列出每個已註冊的任務，
    /// 環境設定檔只寫要改的欄位；缺任務、缺必填欄位或鍵不是已註冊的任務都讓啟動失敗（<see cref="Engine.ScheduledTaskCatalog"/>）。
    /// </summary>
    public Dictionary<string, ScheduledTaskSettings> Tasks { get; set; } = new(StringComparer.Ordinal);

    private static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromDays(1);

    /// <summary>這個程序是否執行指定的任務。</summary>
    public bool ExecutesTask(string key) => Execute && (ExecuteOnly.Count == 0 || ExecuteOnly.Contains(key, StringComparer.Ordinal));

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (key, settings) in Tasks)
        {
            var path = $"{nameof(Tasks)}:{key}";
            if (settings.Schedule is { } schedule && Schedule.Parse(schedule) is null)
                yield return new($"{path}:Schedule '{schedule}' must be an interval between {Schedule.MinInterval:c} and {Schedule.MaxInterval:c} (e.g. 00:05:00) or a five-field cron expression (e.g. 30 3 * * *).", [path]);
            if (settings.Timeout is { } timeout && (timeout < MinTimeout || timeout > MaxTimeout))
                yield return new($"{path}:Timeout must be between {MinTimeout:c} and {MaxTimeout:c}.", [path]);
        }
    }
}

/// <summary>單一任務的設定。</summary>
public sealed class ScheduledTaskSettings
{
    /// <summary>false 時不執行（依排程與手動都不行），管理頁顯示為設定停用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 必填（排程由任務性質決定的啟動任務除外，且不能填）：固定間隔或五欄 cron，見 <see cref="global::AiNexus.Platform.Scheduling.Schedule.Parse"/>。
    /// 兩種寫在同一個鍵，環境設定檔覆寫時整個取代，不會和 appsettings.json 的另一種同時存在。
    /// </summary>
    public string? Schedule { get; set; }

    /// <summary>必填：一輪的上限，超過就取消並記為失敗，避免卡住的外部呼叫一直佔著租約。</summary>
    public TimeSpan? Timeout { get; set; }
}

[OptionsValidator]
public sealed partial class SchedulingOptionsValidator : IValidateOptions<SchedulingOptions>;
