using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments;

public static class AttachmentConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        AiNexus.Features.AccessControl.PlatformFeatures.Add(model, "files", "檔案庫", "/files", 15);
        var file = model.Entity<Attachment>();
        file.ToTable("Attachments", "attachments", table =>
        {
            table.HasCheckConstraint("CK_Attachments_Size", "[Size] > 0");
            table.HasCheckConstraint("CK_Attachments_StorageState", "[StorageState] IN ('pending', 'ready', 'deleting')");
        });
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).HasMaxLength(180);
        file.Property(x => x.ContentType).HasMaxLength(80);
        file.Property(x => x.StorageKey).HasMaxLength(32);
        file.Property(x => x.StorageState).HasMaxLength(16);
        file.HasIndex(x => x.StorageKey).IsUnique();
        file.HasIndex(x => new { x.StorageState, x.CreatedAt });
        file.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        file.HasIndex(x => new { x.OwnerId, x.InLibrary, x.CreatedAt, x.Id });
        file.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var link = model.Entity<MessageAttachment>();
        link.ToTable("MessageAttachments", "attachments");
        link.HasKey(x => new { x.MessageId, x.AttachmentId });
        link.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
