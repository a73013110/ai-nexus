using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Audit;

public sealed class AuditOptions
{
    public const string Section = "Audit";

    /// <summary>單次匯出的筆數上限；超過時拒絕匯出，不截斷。</summary>
    [Range(1, 100000)] public int MaxExportRows { get; set; } = 10000;
}

[OptionsValidator]
public sealed partial class AuditOptionsValidator : IValidateOptions<AuditOptions>;
