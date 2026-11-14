using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Flow;

public sealed class FlowOptions
{
    public const string Section = "Flow";

    /// <summary>送出後超過這個天數仍未結束的申請改為逾期（由 <c>flow.maintenance</c> 處理）；申請人要重新送出。</summary>
    [Range(1, 365)] public int ExpireAfterDays { get; set; } = 30;

    /// <summary>關卡等待超過這個天數，每天提醒一次審核人（<c>flow.reminders</c>）。</summary>
    [Range(1, 30)] public int RemindAfterDays { get; set; } = 2;
}

[OptionsValidator]
public sealed partial class FlowOptionsValidator : IValidateOptions<FlowOptions>;
