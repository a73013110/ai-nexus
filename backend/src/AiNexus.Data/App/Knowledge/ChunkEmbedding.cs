using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>
/// 向量表共用的欄位。每個支援的維度各有一張表（<c>vector(n)</c> 欄位不能混放不同維度），結構相同、只差維度。
/// 這個基底型別不在 EF 模型裡，所以沒有繼承對應或鑑別欄位；新增維度＝新增一個子類與一行組態。
/// </summary>
public abstract class ChunkEmbedding
{
    public int Id { get; set; }
    public Guid ChunkId { get; set; }
    public int ProfileId { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public SqlVector<float> Vector { get; set; }
}

internal abstract class ChunkEmbeddingConfiguration<T>(string table, int dimensions) : IEntityTypeConfiguration<T> where T : ChunkEmbedding
{
    public void Configure(EntityTypeBuilder<T> item)
    {
        item.ToTable(table, "knowledge");
        item.HasKey(x => x.Id);
        item.Property(x => x.ContentHash).HasColumnType("binary(32)");
        item.Property(x => x.Vector).HasColumnType($"vector({dimensions})");
        item.HasOne<Chunk>().WithMany().HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne<EmbeddingProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
