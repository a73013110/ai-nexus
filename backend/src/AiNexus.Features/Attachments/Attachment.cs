using AiNexus.Features.AccessControl;
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
