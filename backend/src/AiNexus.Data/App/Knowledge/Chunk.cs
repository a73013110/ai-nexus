using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>知識庫文件的一段索引片段；<see cref="SearchId"/> 是全文索引使用的鍵。</summary>
public sealed class Chunk
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

internal sealed class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> chunk)
    {
        chunk.ToTable("Chunks", "knowledge");
        chunk.HasKey(x => x.Id);
        chunk.Property(x => x.Text).HasMaxLength(4000);
        chunk.Property(x => x.HeadingPath).HasMaxLength(400);
        chunk.Property(x => x.ContentHash).HasColumnType("binary(32)");
        chunk.Property(x => x.SearchId).UseIdentityColumn();
        chunk.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
