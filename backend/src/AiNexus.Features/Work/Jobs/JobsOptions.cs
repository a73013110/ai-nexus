using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Jobs;

public sealed class JobsOptions
{
    public const string Section = "Jobs";

    /// <summary>已結束的佇列工作保存天數；超過且沒有資料引用的由 <see cref="Retention.JobRetentionTask"/> 刪除。下限 30 天，避免設錯值一次清光。</summary>
    [Range(30, 3650)] public int RetentionDays { get; set; } = 180;
}

[OptionsValidator]
public sealed partial class JobsOptionsValidator : IValidateOptions<JobsOptions>;
