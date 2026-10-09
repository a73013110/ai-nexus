using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Retrieval;

/// <summary>A numbered source of an answer, frozen when the answer was generated.</summary>
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
        citation.ToTable("MessageCitations", "knowledge"); citation.HasKey(x => new { x.MessageId, x.Number }); citation.Property(x => x.Title).HasMaxLength(180); citation.Property(x => x.Excerpt).HasMaxLength(800);
        citation.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
