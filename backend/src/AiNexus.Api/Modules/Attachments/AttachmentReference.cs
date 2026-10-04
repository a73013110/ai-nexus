using AiNexus.Modules.Collaboration;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Attachments;

// A resource pins its original file independently of chat history and browser drafts.
public sealed class AttachmentReference
{
    public Guid ResourceId { get; set; }
    public Guid AttachmentId { get; set; }
}
public static class AttachmentReferenceConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var link = model.Entity<AttachmentReference>(); link.ToTable("ResourceAttachments", "attachments"); link.HasKey(x => new { x.ResourceId, x.AttachmentId });
        link.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
