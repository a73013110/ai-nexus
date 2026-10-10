using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Monitoring;

public sealed class MonitoringOptions
{
    public const string Section = "Monitoring";

    public bool Enabled { get; set; } = true;
    [Range(100, 10000)] public int MaxSessions { get; set; } = 2000;
    [Range(60, 300)] public int SessionTimeoutSeconds { get; set; } = 90;
    [Range(2, 15)] public int RefreshSeconds { get; set; } = 3;
}

[OptionsValidator]
public sealed partial class MonitoringOptionsValidator : IValidateOptions<MonitoringOptions>;
