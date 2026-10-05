namespace AiNexus.Modules.Attachments;

public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public byte[] Data { get; set; } = [];
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
    public int MaxOwnerBytes { get; set; } = 64 * 1024 * 1024;
    public int MaxExtractedCharacters { get; set; } = 64000;
    public int MaxPdfPages { get; set; } = 40;
    public int ImageTokenEstimate { get; set; } = 4096;
    public int DraftRetentionDays { get; set; } = 14;
}

// Serializes quota checks and attachment writes; provider calls never hold this gate.
public sealed class AttachmentWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
