using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Sharing;

/// <summary>A frozen snapshot of a conversation branch or artifact version, readable only by its owner and named recipients.</summary>
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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> link)
    {
        link.ToTable("ShareLinks", "collaboration"); link.HasKey(x => x.Id);
        link.Property(x => x.Kind).HasMaxLength(24); link.Property(x => x.Title).HasMaxLength(120);
        link.HasIndex(x => new { x.OwnerId, x.CreatedAt }); link.HasIndex(x => x.ExpiresAt);
    }
}

/// <summary>Entry point used by <c>NexusDbContext</c>.</summary>
public static class SharingConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new ShareLinkConfiguration());
        model.ApplyConfiguration(new ShareRecipientConfiguration());
    }
}
