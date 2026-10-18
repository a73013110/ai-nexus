namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed record EmbeddingWrite(Guid ChunkId, byte[] ContentHash, float[] Vector);
