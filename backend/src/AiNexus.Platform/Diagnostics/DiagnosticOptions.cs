using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

public sealed class DiagnosticOptions : IValidatableObject
{
    public const string Section = "Diagnostics";

    public string Directory { get; set; } = "";
    [Length(1, 80)] public string ServiceName { get; set; } = "AiNexus";
    [EnumDataType(typeof(LogLevel)), DeniedValues(LogLevel.None)] public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    [EnumDataType(typeof(LogLevel)), DeniedValues(LogLevel.None)] public LogLevel FrameworkMinimumLevel { get; set; } = LogLevel.Warning;
    [Range(1, 100000)] public int QueueCapacity { get; set; } = 8192;
    [Range(1, 20000)] public int ImportantQueueCapacity { get; set; } = 2048;
    [Range(1, 1000)] public int BatchSize { get; set; } = 200;
    [Range(10, 10000)] public int FlushIntervalMs { get; set; } = 1000;
    [Range(1, 300)] public int RetrySeconds { get; set; } = 10;
    [Range(1, 30)] public int SqlTimeoutSeconds { get; set; } = 5;
    [Range(1, 30)] public int ShutdownSeconds { get; set; } = 10;
    [Range(65536d, 268435456d)] public long FileSizeBytes { get; set; } = 16 * 1024 * 1024;
    [Range(65536d, 1099511627776d)] public long MaxDiskBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    [Range(1000d, 1_000_000_000d)] public long MaxSqlRows { get; set; } = 5_000_000;
    [Range(1, 365)] public int FileRetentionDays { get; set; } = 14;
    [Range(1, 3650)] public int RetentionDays { get; set; } = 30;
    [Range(30, 36500)] public int AuditRetentionDays { get; set; } = 365;
    [Range(1, 10000)] public int CleanupBatchSize { get; set; } = 1000;
    [Range(1, 1000)] public int LowLevelSampleEvery { get; set; } = 1;
    [Range(1, 366)] public int MaxQueryDays { get; set; } = 31;
    [Range(1, 10000)] public int MaxExportRows { get; set; } = 5000;
    [Range(1, 31)] public int MaxExportDays { get; set; } = 7;
    public bool OtlpEnabled { get; set; }
    [Required, HttpEndpoint] public string OtlpEndpoint { get; set; } = "http://localhost:4318";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxDiskBytes < FileSizeBytes) yield return new($"{nameof(MaxDiskBytes)} must not be less than {nameof(FileSizeBytes)}.", [nameof(MaxDiskBytes)]);
    }

    /// <summary>
    /// Reads the section before dependency injection exists (startup failure journal, exporter wiring). Malformed
    /// settings fall back to the defaults here because options validation reports them when the host starts.
    /// </summary>
    public static DiagnosticOptions Read(IConfiguration configuration)
    {
        try
        {
            var options = configuration.GetSection(Section).Get<DiagnosticOptions>() ?? new();
            return new DiagnosticOptionsValidator().Validate(null, options).Succeeded ? options : new();
        }
        catch (Exception) { return new(); }
    }
}

[OptionsValidator]
public sealed partial class DiagnosticOptionsValidator : IValidateOptions<DiagnosticOptions>;
