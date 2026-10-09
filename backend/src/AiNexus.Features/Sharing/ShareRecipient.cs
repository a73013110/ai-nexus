using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Sharing;

public sealed class ShareRecipient { public Guid ShareId { get; set; } public Guid UserId { get; set; } }

internal sealed class ShareRecipientConfiguration : IEntityTypeConfiguration<ShareRecipient>
{
    public void Configure(EntityTypeBuilder<ShareRecipient> recipient)
    {
        recipient.ToTable("ShareRecipients", "collaboration"); recipient.HasKey(x => new { x.ShareId, x.UserId });
        recipient.HasIndex(x => new { x.UserId, x.ShareId });
        recipient.HasOne<ShareLink>().WithMany().HasForeignKey(x => x.ShareId).OnDelete(DeleteBehavior.Cascade);
    }
}
