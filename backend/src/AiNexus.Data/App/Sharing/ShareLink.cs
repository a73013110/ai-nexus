using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Sharing;

/// <summary>凍結的對話分支或成果版本快照，只有擁有者與指定收件人能讀取。</summary>
public sealed class ShareLink
{
    public const int MaxActivePerOwner = 100;
    public const int MaxRecipients = 20;
    public const int MaxHours = 720;
    public const int MaxMessages = 100;
    public const int MaxContentCharacters = 256000;

    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid SourceId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public bool IncludeAttachments { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> link)
    {
        link.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        link.ToTable("ShareLinks", "sharing");
        link.HasKey(x => x.Id);
        link.Property(x => x.Kind).HasMaxLength(24);
        link.Property(x => x.Title).HasMaxLength(120);
        link.HasOne<Resource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
