using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Sharing;

[Comment("分享的具名收件人；與原資源 ACL 分開判定。")]
public sealed class ShareRecipient
{
    [Comment("關聯分享的識別碼。")]
    public Guid ShareId { get; set; }
    [Comment("關聯使用者的 Users 主鍵。")]
    public Guid UserId { get; set; }
}

internal sealed class ShareRecipientConfiguration : IEntityTypeConfiguration<ShareRecipient>
{
    public void Configure(EntityTypeBuilder<ShareRecipient> recipient)
    {
        recipient.ToTable("ShareRecipients", "sharing"); recipient.HasKey(x => new { x.ShareId, x.UserId });
        recipient.HasIndex(x => new { x.UserId, x.ShareId });
        recipient.HasOne<ShareLink>().WithMany().HasForeignKey(x => x.ShareId).OnDelete(DeleteBehavior.Cascade);
    }
}
