using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

// A resource pins its original file independently of chat history and browser drafts.
public sealed class AttachmentReference
{
    public Guid ResourceId { get; set; }
    public Guid AttachmentId { get; set; }
}

internal sealed class AttachmentReferenceEntityConfiguration : IEntityTypeConfiguration<AttachmentReference>
{
    public void Configure(EntityTypeBuilder<AttachmentReference> link)
    {
        link.ToTable("ResourceAttachments", "attachments"); link.HasKey(x => new { x.ResourceId, x.AttachmentId });
        link.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class AttachmentReferenceConfiguration
{
    public static void Configure(ModelBuilder model) => model.ApplyConfiguration(new AttachmentReferenceEntityConfiguration());
}
