using AiNexus.Data.App.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>對話所有者為該對話選定、用於回答的知識庫（最多三個）。</summary>
public sealed class ConversationCollection
{
    public Guid ConversationId { get; set; }
    public Guid CollectionId { get; set; }
}

internal sealed class ConversationCollectionConfiguration : IEntityTypeConfiguration<ConversationCollection>
{
    public void Configure(EntityTypeBuilder<ConversationCollection> link)
    {
        link.ToTable("ConversationCollections", "knowledge");
        link.HasKey(x => new { x.ConversationId, x.CollectionId });
        link.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
