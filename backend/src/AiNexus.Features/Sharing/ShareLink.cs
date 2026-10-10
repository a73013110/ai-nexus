using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Sharing;

/// <summary>A frozen snapshot of a conversation branch or artifact version, readable only by its owner and named recipients.</summary>
[Comment("分享版本快照、有效期限、附件選項及撤銷狀態。")]
public sealed class ShareLink
{
    public const int MaxActivePerOwner = 100;
    public const int MaxRecipients = 20;
    public const int MaxHours = 720;
    public const int MaxMessages = 100;
    public const int MaxContentCharacters = 256000;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("外部資料來源識別碼。")]
    public Guid SourceId { get; set; }
    [Comment("分享內容的種類。")]
    public string Kind { get; set; } = "";
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "";
    [Comment("分享時的固定內容快照；不隨後續編輯變動。")]
    public string SnapshotJson { get; set; } = "";
    [Comment("是否明確允許分享附件。")]
    public bool IncludeAttachments { get; set; }
    [Comment("分享是否已撤銷。")]
    public bool IsRevoked { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
    [Comment("分享到期時間。")]
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> link)
    {
        link.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        link.ToTable("ShareLinks", "sharing"); link.HasKey(x => x.Id);
        link.Property(x => x.Kind).HasMaxLength(24); link.Property(x => x.Title).HasMaxLength(120);
        link.HasIndex(x => new { x.OwnerId, x.CreatedAt }); link.HasIndex(x => x.ExpiresAt);
    }
}
