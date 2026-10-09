namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed record RetrievalMetricDto(string CaseId, string Mode, string ActualMode, string? Unavailable, double? Recall, double? ReciprocalRank, double? Ndcg, bool? Refused, long RewriteMs, long EmbedMs, long SearchMs, long RerankMs, long ElapsedMs);
