using Microsoft.Data.SqlTypes;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class ChunkEmbedding1024
{
    public int Id { get; set; }
    public Guid ChunkId { get; set; }
    public int ProfileId { get; set; }
    public byte[] ContentHash { get; set; } = [];
    public SqlVector<float> Vector { get; set; }
}
