namespace AiNexus.Features.Attachments;

public sealed class AttachmentOptions
{
    public int MaxFileBytes { get; set; } = 4 * 1024 * 1024;
    public int MaxFilesPerMessage { get; set; } = 4;
    public int MaxMessageBytes { get; set; } = 8 * 1024 * 1024;
    public const long DefaultLimitBytes = 5_000_000_000;
    public const long MaximumLimitBytes = 1_000_000_000_000_000;
    public long DefaultOwnerLimitBytes { get; set; } = DefaultLimitBytes;
    public string StoragePath { get; set; } = "";
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int MaxExtractedCharacters { get; set; } = 64000;
    public int MaxPdfPages { get; set; } = 40;
    public int ImageTokenEstimate { get; set; } = 4096;
    public int DraftRetentionDays { get; set; } = 14;
}
