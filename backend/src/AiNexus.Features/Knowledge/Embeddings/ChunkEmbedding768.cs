using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge.Embeddings;

[Comment("768 維原生向量、片段關聯與 profile 內容快取。")]
public sealed class ChunkEmbedding768
{
    [Comment("資料的主鍵識別碼。")]
    public int Id { get; set; }
    [Comment("向量對應的結構化片段 Guid 識別碼。")]
    public Guid ChunkId { get; set; }
    [Comment("向量空間及切段版本的 EmbeddingProfiles 外鍵。")]
    public int ProfileId { get; set; }
    [Comment("實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。")]
    public byte[] ContentHash { get; set; } = [];
    [Comment("L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。")]
    public SqlVector<float> Vector { get; set; }
}

internal sealed class ChunkEmbedding768Configuration() : ChunkEmbeddingConfiguration<ChunkEmbedding768>("ChunkEmbeddings768", 768);
