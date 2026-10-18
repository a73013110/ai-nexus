using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Sharing;

public sealed class ShareRecipient
{
    public Guid ShareId { get; set; }
    public Guid UserId { get; set; }
}

internal sealed class ShareRecipientConfiguration : IEntityTypeConfiguration<ShareRecipient>
{
    public void Configure(EntityTypeBuilder<ShareRecipient> recipient)
    {
        recipient.ToTable("ShareRecipients", "sharing");
        recipient.HasKey(x => new { x.ShareId, x.UserId });
        recipient.HasOne<ShareLink>().WithMany().HasForeignKey(x => x.ShareId).OnDelete(DeleteBehavior.Cascade);
        recipient.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
