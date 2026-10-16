using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Audit;

public sealed class AuditOptions
{
    public const string Section = "Audit";

    /// <summary>單次匯出的筆數上限；超過時拒絕匯出，不截斷。</summary>
    [Range(1, 100000)] public int MaxExportRows { get; set; } = 10000;

    /// <summary>匯出查詢的 SQL 逾時秒數；不限時間的文字搜尋會掃描詳情欄位，不能無限等待。</summary>
    [Range(1, 300)] public int ExportTimeoutSeconds { get; set; } = 30;
}

[OptionsValidator]
public sealed partial class AuditOptionsValidator : IValidateOptions<AuditOptions>;
