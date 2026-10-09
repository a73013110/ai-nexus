using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge.Documents;

[Comment("文件逐頁擷取的文字、頁碼與 OCR 結果。")]
public sealed class DocumentPage
{
    [Comment("關聯知識文件的識別碼。")]
    public Guid DocumentId { get; set; }
    [Comment("文件頁碼，從 1 開始。")]
    public int PageNumber { get; set; }
    [Comment("文件頁面／片段的擷取文字。")]
    public string Text { get; set; } = "";
    [Comment("附件文字擷取方法或結果。")]
    public string Extraction { get; set; } = "native";
    [Comment("此評測結果是否需要人工覆核。")]
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
