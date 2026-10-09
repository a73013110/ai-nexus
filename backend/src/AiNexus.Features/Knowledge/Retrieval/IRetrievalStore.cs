using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public interface IRetrievalStore
{
    Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct);
}

public sealed class RetrievalRow
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = "";
    public int PageNumber { get; set; }
    public int EndPage { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = "";
    public string HeadingPath { get; set; } = "";
    public double Score { get; set; }
    public int? VectorRank { get; set; }
    public int? FtsRank { get; set; }
    public double? VectorScore { get; set; }
    public double? RrfScore { get; set; }
    public KnowledgeHitDto Hit() => new(DocumentId, Title, PageNumber, Text, Score, ChunkId, EndPage, Ordinal, VectorRank, FtsRank, VectorScore, RrfScore, HeadingPath: HeadingPath);
}
