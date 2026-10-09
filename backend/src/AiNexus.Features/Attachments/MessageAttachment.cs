using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

[Comment("訊息與附件的關聯及呈現順序。")]
public sealed class MessageAttachment
{
    [Comment("關聯訊息的識別碼。")]
    public Guid MessageId { get; set; }
    [Comment("引用的附件識別碼。")]
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
