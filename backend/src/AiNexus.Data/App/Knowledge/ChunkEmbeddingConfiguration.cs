using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>每個支援的向量維度各有一張向量表。</summary>
internal abstract class ChunkEmbeddingConfiguration<T>(string table, int dimensions) : IEntityTypeConfiguration<T> where T : class
{
    public void Configure(EntityTypeBuilder<T> item)
    {
        item.ToTable(table, "knowledge"); item.HasKey("Id");
        item.Property<int>("Id").ValueGeneratedOnAdd(); item.Property<byte[]>("ContentHash").HasColumnType("binary(32)");
        item.HasOne<Chunk>().WithMany().HasForeignKey("ChunkId").OnDelete(DeleteBehavior.Cascade);
        item.HasOne<EmbeddingProfile>().WithMany().HasForeignKey("ProfileId").OnDelete(DeleteBehavior.Restrict);
        item.Property<SqlVector<float>>("Vector").HasColumnType($"vector({dimensions})");
    }
}
