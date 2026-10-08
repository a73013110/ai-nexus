namespace AiNexus.Features.Attachments;

public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string StorageKey { get; set; } = Guid.NewGuid().ToString("N");
    public string StorageState { get; set; } = AttachmentStates.Ready;
    public string? ExtractedText { get; set; }
    public bool InLibrary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MessageAttachment
{
    public Guid MessageId { get; set; }
    public Guid AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, bool IsImage, string AnalysisMode);
public sealed record AttachmentPolicyDto(long MaxFileBytes, int MaxFilesPerMessage, long MaxMessageBytes, string[] Extensions);

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

// Serializes quota checks and attachment writes; provider calls never hold this gate.
public sealed class AttachmentWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }

public static class AttachmentStates
{
    public const string Pending = "pending";
    public const string Ready = "ready";
    public const string Deleting = "deleting";
}

public sealed record AttachmentStorageDto(long UsedBytes, long LimitBytes, long RemainingBytes, long? PersonalLimitBytes, long? GroupLimitBytes, long DefaultLimitBytes, string LimitSource);
public sealed record AttachmentStorageLimitRequest(long? LimitBytes);
