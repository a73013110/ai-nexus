using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

// A resource pins its original file independently of chat history and browser drafts.
[Comment("知識庫或專案資源與附件的引用關聯。")]
public sealed class AttachmentReference
{
    [Comment("關聯 Resources 的識別碼。")]
    public Guid ResourceId { get; set; }
    [Comment("引用的附件識別碼。")]
    public Guid AttachmentId { get; set; }
}

internal sealed class AttachmentReferenceConfiguration : IEntityTypeConfiguration<AttachmentReference>
{
    public void Configure(EntityTypeBuilder<AttachmentReference> link)
    {
        link.ToTable("ResourceAttachments", "attachments"); link.HasKey(x => new { x.ResourceId, x.AttachmentId });
        link.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
