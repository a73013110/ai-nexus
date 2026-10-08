using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Errors;
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

public sealed class ShareRecipient { public Guid ShareId { get; set; } public Guid UserId { get; set; } }

public sealed record ShareDto(Guid Id, string Kind, string Title, string Owner, bool IsOwner, bool IsRevoked, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt, IReadOnlyList<string> Recipients, bool IncludeAttachments);
public sealed record SharedMessageDto(string Role, string Content, string Status, DateTimeOffset CreatedAt, IReadOnlyList<AttachmentDto> Attachments,
    string? ModelId = null, string? ErrorCode = null, IReadOnlyList<CitationDto>? Sources = null, IReadOnlyList<WebSourceDto>? WebSources = null, RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);
public sealed record SharedFilePreviewDto(AttachmentDto File, IReadOnlyList<DocumentPageDto> Pages);
public sealed record ShareSnapshot(string Content, int? ArtifactVersion, IReadOnlyList<SharedMessageDto> Messages);
public sealed record SharedContentDto(ShareDto Share, ShareSnapshot Snapshot);

/// <summary>Serializes share creation and revocation in this process.</summary>
public sealed class ShareWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }

internal static class SharingErrors
{
    public const string InvalidCode = "share_invalid";
    public static readonly Error RecipientUnknown = Error.Invalid("share_recipient_unknown");
    public static readonly Error LimitReached = Error.Conflict("share_limit");
    public static readonly Error GenerationActive = Error.Conflict("share_generation_active");
    public static readonly Error HistoryInvalid = Error.Conflict("share_history_invalid");
    public static readonly Error HistoryLimit = Error.Conflict("share_history_limit");
    public static readonly Error ContentLimit = Error.Conflict("share_content_limit");

    /// <summary>One answer for missing, expired, revoked and not-addressed-to-you, so recipients cannot probe other shares.</summary>
    public static readonly Error Unavailable = Error.NotFound("share_unavailable");
}

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> link)
    {
        link.ToTable("ShareLinks", "collaboration"); link.HasKey(x => x.Id);
        link.Property(x => x.Kind).HasMaxLength(24); link.Property(x => x.Title).HasMaxLength(120);
        link.HasIndex(x => new { x.OwnerId, x.CreatedAt }); link.HasIndex(x => x.ExpiresAt);
        link.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShareRecipientConfiguration : IEntityTypeConfiguration<ShareRecipient>
{
    public void Configure(EntityTypeBuilder<ShareRecipient> recipient)
    {
        recipient.ToTable("ShareRecipients", "collaboration"); recipient.HasKey(x => new { x.ShareId, x.UserId });
        recipient.HasIndex(x => new { x.UserId, x.ShareId });
        recipient.HasOne<ShareLink>().WithMany().HasForeignKey(x => x.ShareId).OnDelete(DeleteBehavior.Cascade);
        recipient.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
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
