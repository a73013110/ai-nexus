using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Embeddings;

/// <summary>One vector table per supported dimension; SQLite test databases store the vector as a blob.</summary>
internal sealed class ChunkEmbeddingConfiguration<T>(string table, int dimensions, bool sqlite) : IEntityTypeConfiguration<T> where T : class
{
    public void Configure(EntityTypeBuilder<T> item)
    {
        item.ToTable(table, "knowledge"); item.HasKey("Id").IsClustered();
        item.Property<int>("Id").ValueGeneratedOnAdd(); item.Property<byte[]>("ContentHash").HasColumnType("binary(32)");
        item.HasIndex("ProfileId", "ChunkId").IsUnique(); item.HasIndex("ProfileId", "ContentHash");
        item.HasOne<KnowledgeChunk>().WithMany().HasForeignKey("ChunkId").OnDelete(DeleteBehavior.Cascade);
        item.HasOne<EmbeddingProfile>().WithMany().HasForeignKey("ProfileId").OnDelete(DeleteBehavior.Restrict);
        if (sqlite) item.Property<SqlVector<float>>("Vector").HasConversion(new ValueConverter<SqlVector<float>, byte[]>(v => VectorBytes.Write(v), b => VectorBytes.Read(b))).HasColumnType("BLOB");
        else item.Property<SqlVector<float>>("Vector").HasColumnType($"vector({dimensions})");
    }
}
