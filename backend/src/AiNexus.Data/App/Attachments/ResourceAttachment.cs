using AiNexus.Data.App.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attachments;

// 資源會獨立持有原始檔案，不依賴對話紀錄或瀏覽器草稿。
public sealed class ResourceAttachment
{
    public Guid ResourceId { get; set; }
    public Guid AttachmentId { get; set; }
}

internal sealed class ResourceAttachmentConfiguration : IEntityTypeConfiguration<ResourceAttachment>
{
    public void Configure(EntityTypeBuilder<ResourceAttachment> link)
    {
        link.ToTable("ResourceAttachments", "attachments");
        link.HasKey(x => new { x.ResourceId, x.AttachmentId });
        link.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<Resource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
