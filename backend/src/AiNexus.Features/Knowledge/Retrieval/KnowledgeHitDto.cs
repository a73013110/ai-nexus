namespace AiNexus.Features.Knowledge.Retrieval;

public sealed record KnowledgeHitDto(Guid DocumentId, string Title, int PageNumber, string Text, double Score,
    Guid ChunkId, int EndPage = 0, int Ordinal = 0, int? VectorRank = null, int? FtsRank = null,
    double? VectorScore = null, double? RrfScore = null, double? RerankScore = null, string HeadingPath = "");

public sealed record RerankScore(int Index, double Score);
