using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>A collection the owner of a conversation selected for its answers (at most three).</summary>
[Comment("對話選定的知識庫來源關聯。")]
public sealed class ConversationKnowledge
{
    [Comment("關聯對話的識別碼。")]
    public Guid ConversationId { get; set; }
    [Comment("關聯知識庫的識別碼。")]
    public Guid CollectionId { get; set; }
}

internal sealed class ConversationKnowledgeConfiguration : IEntityTypeConfiguration<ConversationKnowledge>
{
    public void Configure(EntityTypeBuilder<ConversationKnowledge> link)
    {
        link.ToTable("ConversationCollections", "knowledge"); link.HasKey(x => new { x.ConversationId, x.CollectionId });
        link.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
