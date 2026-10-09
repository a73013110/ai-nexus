using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Retrieval;

/// <summary>A numbered source of an answer, frozen when the answer was generated.</summary>
[Comment("回答生成當時的知識引用、文件頁碼與摘要快照。")]
public sealed class MessageCitation
{
    [Comment("關聯訊息的識別碼。")]
    public Guid MessageId { get; set; }
    [Comment("回答引用的順序編號。")]
    public int Number { get; set; }
    [Comment("關聯知識文件的識別碼。")]
    public Guid DocumentId { get; set; }
    [Comment("文件頁碼，從 1 開始。")]
    public int PageNumber { get; set; }
    [Comment("片段結束的原始文件頁碼。")]
    public int EndPage { get; set; }
    [Comment("介面顯示標題。")]
    public string Title { get; set; } = "";
    [Comment("檢索或引用時保存的文字摘要。")]
    public string Excerpt { get; set; } = "";
}

internal sealed class MessageCitationConfiguration : IEntityTypeConfiguration<MessageCitation>
{
    public void Configure(EntityTypeBuilder<MessageCitation> citation)
    {
        citation.ToTable("MessageCitations", "knowledge"); citation.HasKey(x => new { x.MessageId, x.Number }); citation.Property(x => x.Title).HasMaxLength(180); citation.Property(x => x.Excerpt).HasMaxLength(800);
        citation.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
