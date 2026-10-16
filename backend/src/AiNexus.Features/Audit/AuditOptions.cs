using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Audit;

public sealed class AuditOptions
{
    public const string Section = "Audit";

    /// <summary>The most records one export may hold; a larger result is refused rather than cut short.</summary>
    [Range(1, 100000)] public int MaxExportRows { get; set; } = 10000;
}

[OptionsValidator]
public sealed partial class AuditOptionsValidator : IValidateOptions<AuditOptions>;
