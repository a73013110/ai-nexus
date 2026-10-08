using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// A shared knowledge collection. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same
/// id; its documents are <see cref="KnowledgeDocument"/> rows that point back to it.
/// </summary>
public sealed class KnowledgeCollection
{
    public const string Kind = "knowledge";
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
}

public sealed record CollectionDto(ResourceDto Resource, string Description, int Documents, int ReadyDocuments);

internal sealed class KnowledgeCollectionConfiguration : IEntityTypeConfiguration<KnowledgeCollection>
{
    public void Configure(EntityTypeBuilder<KnowledgeCollection> collection)
    {
        collection.ToTable("Collections", "knowledge"); collection.HasKey(x => x.Id); collection.Property(x => x.Description).HasMaxLength(KnowledgeCollection.DescriptionMaxLength);
        collection.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>. SQLite test databases store vectors as blobs and number chunks themselves.</summary>
public static class KnowledgeConfiguration
{
    public static void Configure(ModelBuilder model, bool sqlite = false)
    {
        model.ApplyConfiguration(new KnowledgeCollectionConfiguration());
        model.ApplyConfiguration(new KnowledgeDocumentConfiguration());
        model.ApplyConfiguration(new DocumentPageConfiguration());
        model.ApplyConfiguration(new KnowledgeChunkConfiguration(sqlite));
        model.ApplyConfiguration(new EmbeddingProfileConfiguration());
        model.ApplyConfiguration(new ChunkEmbeddingConfiguration<ChunkEmbedding768>("ChunkEmbeddings768", 768, sqlite));
        model.ApplyConfiguration(new ChunkEmbeddingConfiguration<ChunkEmbedding1024>("ChunkEmbeddings1024", 1024, sqlite));
        model.ApplyConfiguration(new ConversationKnowledgeConfiguration());
        model.ApplyConfiguration(new MessageCitationConfiguration());
    }
}
