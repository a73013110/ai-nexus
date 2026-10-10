using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

public sealed class AttachmentOptions : IValidatableObject
{
    public const string Section = "Attachments";
    public const long DefaultLimitBytes = 5_000_000_000;
    public const long MaximumLimitBytes = 1_000_000_000_000_000;

    [Range(1024, 8 * 1024 * 1024)] public int MaxFileBytes { get; set; } = 4 * 1024 * 1024;
    [Range(1, 8)] public int MaxFilesPerMessage { get; set; } = 4;
    [Range(1024, 16 * 1024 * 1024)] public int MaxMessageBytes { get; set; } = 8 * 1024 * 1024;
    public long DefaultOwnerLimitBytes { get; set; } = DefaultLimitBytes;
    /// <summary>Absolute directory outside the site; Development defaults to <c>.local/data/attachments</c>.</summary>
    public string StoragePath { get; set; } = "";
    [Range(1, 1440)] public int CleanupIntervalMinutes { get; set; } = 60;
    [Range(1000, 256000)] public int MaxExtractedCharacters { get; set; } = 64000;
    [Range(1, 100)] public int MaxPdfPages { get; set; } = 40;
    [Range(1024, 16384)] public int ImageTokenEstimate { get; set; } = 4096;
    [Range(1, 365)] public int DraftRetentionDays { get; set; } = 14;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxMessageBytes < MaxFileBytes) yield return new($"{nameof(MaxMessageBytes)} must not be less than {nameof(MaxFileBytes)}.", [nameof(MaxMessageBytes)]);
        if (DefaultOwnerLimitBytes < MaxMessageBytes || DefaultOwnerLimitBytes > MaximumLimitBytes)
            yield return new($"{nameof(DefaultOwnerLimitBytes)} must be between {nameof(MaxMessageBytes)} and {MaximumLimitBytes}.", [nameof(DefaultOwnerLimitBytes)]);
        if (!Path.IsPathFullyQualified(StoragePath)) yield return new($"{nameof(StoragePath)} must be an absolute path outside the site.", [nameof(StoragePath)]);
    }
}

[OptionsValidator]
public sealed partial class AttachmentOptionsValidator : IValidateOptions<AttachmentOptions>;
