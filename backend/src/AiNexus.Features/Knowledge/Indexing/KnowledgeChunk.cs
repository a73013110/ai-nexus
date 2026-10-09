using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Indexing;

/// <summary>One indexed passage of a collection document; <see cref="SearchId"/> is the full-text key.</summary>
public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SearchId { get; set; }
    public Guid DocumentId { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int Ordinal { get; set; }
    public string HeadingPath { get; set; } = "";
    public string Text { get; set; } = "";
    public byte[] ContentHash { get; set; } = [];
    public int TokenEstimate { get; set; }
}

internal sealed class KnowledgeChunkConfiguration(bool sqlite) : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> chunk)
    {
        chunk.ToTable("Chunks", "knowledge"); chunk.HasKey(x => x.Id); chunk.Property(x => x.Text).HasMaxLength(4000);
        chunk.Property(x => x.HeadingPath).HasMaxLength(400); chunk.Property(x => x.ContentHash).HasColumnType("binary(32)");
        if (sqlite) chunk.Property(x => x.SearchId).ValueGeneratedNever(); else chunk.Property(x => x.SearchId).UseIdentityColumn();
        chunk.HasIndex(x => x.SearchId).IsUnique();
        chunk.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique(); chunk.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
