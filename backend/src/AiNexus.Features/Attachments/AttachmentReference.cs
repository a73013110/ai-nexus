using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

// A resource pins its original file independently of chat history and browser drafts.
public sealed class AttachmentReference
{
    public Guid ResourceId { get; set; }
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
