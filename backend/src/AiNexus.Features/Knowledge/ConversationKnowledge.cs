using AiNexus.Features.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge;

/// <summary>A collection the owner of a conversation selected for its answers (at most three).</summary>
public sealed class ConversationKnowledge { public Guid ConversationId { get; set; } public Guid CollectionId { get; set; } }

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

// Also the request body of SaveConversationKnowledge. Its limit (at most three distinct collections) is checked only
// after the conversation is found, inside the generation state gate, so this request has no validator.
public sealed record KnowledgeSelectionDto(IReadOnlyList<Guid> CollectionIds);
public sealed record CitationDto(int Number, Guid DocumentId, string Title, int PageNumber, string Excerpt, int EndPage = 0);

internal sealed class ConversationKnowledgeConfiguration : IEntityTypeConfiguration<ConversationKnowledge>
{
    public void Configure(EntityTypeBuilder<ConversationKnowledge> link)
    {
        link.ToTable("ConversationCollections", "knowledge"); link.HasKey(x => new { x.ConversationId, x.CollectionId });
        link.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade); link.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MessageCitationConfiguration : IEntityTypeConfiguration<MessageCitation>
{
    public void Configure(EntityTypeBuilder<MessageCitation> citation)
    {
        citation.ToTable("MessageCitations", "knowledge"); citation.HasKey(x => new { x.MessageId, x.Number }); citation.Property(x => x.Title).HasMaxLength(180); citation.Property(x => x.Excerpt).HasMaxLength(800);
        citation.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade); citation.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
