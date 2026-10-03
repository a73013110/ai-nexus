using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Attachments;

public static class AttachmentConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var file = model.Entity<Attachment>();
        file.ToTable("Attachments", "attachments");
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).HasMaxLength(180);
        file.Property(x => x.ContentType).HasMaxLength(80);
        file.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        file.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var link = model.Entity<MessageAttachment>();
        link.ToTable("MessageAttachments", "attachments");
        link.HasKey(x => new { x.MessageId, x.AttachmentId });
        link.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
