using AiNexus.Data.App.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>回答中的編號來源，於回答生成當下凍結，之後不隨文件內容變動。</summary>
public sealed class MessageCitation
{
    public Guid MessageId { get; set; }
    public int Number { get; set; }
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public int EndPage { get; set; }
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
}

internal sealed class MessageCitationConfiguration : IEntityTypeConfiguration<MessageCitation>
{
    public void Configure(EntityTypeBuilder<MessageCitation> citation)
    {
        citation.ToTable("MessageCitations", "knowledge");
        citation.HasKey(x => new { x.MessageId, x.Number });
        citation.Property(x => x.Title).HasMaxLength(180);
        citation.Property(x => x.Excerpt).HasMaxLength(800);
        citation.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        citation.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
