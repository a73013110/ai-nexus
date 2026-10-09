using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge.Documents;

public sealed class DocumentPage
{
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public string Text { get; set; } = "";
    public string Extraction { get; set; } = "native";
    public bool NeedsReview { get; set; }
}

internal sealed class DocumentPageConfiguration : IEntityTypeConfiguration<DocumentPage>
{
    public void Configure(EntityTypeBuilder<DocumentPage> page)
    {
        page.ToTable("DocumentPages", "knowledge"); page.HasKey(x => new { x.DocumentId, x.PageNumber }); page.Property(x => x.Extraction).HasMaxLength(16);
        page.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
