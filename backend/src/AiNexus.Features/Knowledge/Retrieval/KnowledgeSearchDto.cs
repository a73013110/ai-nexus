namespace AiNexus.Features.Knowledge.Retrieval;

public sealed record KnowledgeSearchDto(string Mode, IReadOnlyList<KnowledgeHitDto> Hits, long RewriteMs = 0, long EmbedMs = 0, long SearchMs = 0, long RerankMs = 0);
