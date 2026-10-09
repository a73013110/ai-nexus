using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

public sealed class MessageAttachment
{
    public Guid MessageId { get; set; }
    public Guid AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}

internal sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> link)
    {
        link.ToTable("MessageAttachments", "attachments");
        link.HasKey(x => new { x.MessageId, x.AttachmentId });
        link.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
