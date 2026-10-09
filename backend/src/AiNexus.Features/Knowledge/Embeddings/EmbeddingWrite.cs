namespace AiNexus.Features.Knowledge.Embeddings;

public sealed record EmbeddingWrite(Guid ChunkId, byte[] ContentHash, float[] Vector);
