using AiNexus.Features.AccessControl;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

public sealed class Attachment
{
    public const int FileNameMaxLength = 180;

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

internal static class AttachmentErrors
{
    public const string FileNameInvalidCode = "file_name_invalid";
    public const string StorageLimitInvalidCode = "invalid_storage_limit";

    public static readonly Error NotFound = Error.NotFound("attachment_not_found");
    public static readonly Error InUse = Error.Conflict("attachment_in_use");
    public static readonly Error MultipartRequired = Error.Invalid("multipart_required");
    public static readonly Error FileRequired = Error.Invalid("file_required");
    public static readonly Error LibraryFileNotFound = Error.NotFound("file_not_found");
    public static readonly Error ExtensionChanged = Error.Invalid("file_extension_changed");
    public static readonly Error FileNameChanged = Error.Conflict("file_name_changed");
    public static readonly Error FilterInvalid = Error.Invalid("file_filter_invalid");
}

internal sealed class AttachmentEntityConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> file)
    {
        file.ToTable("Attachments", "attachments", table =>
        {
            table.HasCheckConstraint("CK_Attachments_Size", "[Size] > 0");
            table.HasCheckConstraint("CK_Attachments_StorageState", "[StorageState] IN ('pending', 'ready', 'deleting')");
        });
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).HasMaxLength(Attachment.FileNameMaxLength);
        file.Property(x => x.ContentType).HasMaxLength(80);
        file.Property(x => x.StorageKey).HasMaxLength(32);
        file.Property(x => x.StorageState).HasMaxLength(16);
        file.HasIndex(x => x.StorageKey).IsUnique();
        file.HasIndex(x => new { x.StorageState, x.CreatedAt });
        file.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        file.HasIndex(x => new { x.OwnerId, x.InLibrary, x.CreatedAt, x.Id });
        file.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> link)
    {
        link.ToTable("MessageAttachments", "attachments");
        link.HasKey(x => new { x.MessageId, x.AttachmentId });
        link.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class AttachmentConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        PlatformFeatures.Add(model, "files", "檔案庫", "/files", 15);
        model.ApplyConfiguration(new AttachmentEntityConfiguration());
        model.ApplyConfiguration(new MessageAttachmentConfiguration());
    }
}
