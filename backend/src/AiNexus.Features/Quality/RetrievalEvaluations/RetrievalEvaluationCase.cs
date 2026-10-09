namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed record RetrievalRelevance(Guid DocumentId, IReadOnlyList<int> Pages, int Grade = 1);

public sealed record RetrievalEvaluationCase(string Id, string Query, IReadOnlyList<RetrievalRelevance> Relevant, bool NoAnswer = false);
