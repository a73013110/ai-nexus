using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Knowledge;

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
