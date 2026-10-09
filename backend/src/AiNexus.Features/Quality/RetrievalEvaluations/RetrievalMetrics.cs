using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed record RetrievalLatencyDto(double P50, double P95);

public sealed record RetrievalSummaryDto(string Mode, bool Comparable, int Completed, int Unavailable, double? Recall, double? Mrr, double? Ndcg, double? RefusalRate, RetrievalLatencyDto Rewrite, RetrievalLatencyDto Embed, RetrievalLatencyDto Search, RetrievalLatencyDto Rerank, RetrievalLatencyDto Total);

public static class RetrievalMetrics
{
    public static IReadOnlyList<string> Modes { get; } = Array.AsReadOnly(new[] { "vector", "keyword", "hybrid", "hybrid+rerank" });
    public static (double? Recall, double? ReciprocalRank, double? Ndcg, bool? Refused) Measure(RetrievalEvaluationCase test, IReadOnlyList<KnowledgeHitDto> hits, int k)
    {
        if (test.NoAnswer) return (null, null, null, hits.Count == 0);
        var matched = new HashSet<Guid>(); double dcg = 0, reciprocal = 0;
        for (var rank = 0; rank < Math.Min(k, hits.Count); rank++)
        {
            var hit = hits[rank];
            var relevant = test.Relevant.SingleOrDefault(x => x.DocumentId == hit.DocumentId && (x.Pages.Count == 0 || x.Pages.Any(p => p >= hit.PageNumber && p <= (hit.EndPage > 0 ? hit.EndPage : hit.PageNumber))));
            if (relevant is null || !matched.Add(relevant.DocumentId)) continue;
            if (reciprocal == 0) reciprocal = 1.0 / (rank + 1);
            dcg += (Math.Pow(2, relevant.Grade) - 1) / Math.Log2(rank + 2);
        }
        var ideal = test.Relevant.OrderByDescending(x => x.Grade).Take(k).Select((x, i) => (Math.Pow(2, x.Grade) - 1) / Math.Log2(i + 2)).Sum();
        return ((double)matched.Count / test.Relevant.Count, reciprocal, ideal == 0 ? 0 : dcg / ideal, null);
    }
    public static RetrievalLatencyDto Latency(IEnumerable<long> values)
    {
        var sorted = values.Order().ToArray();
        double Percentile(double percentile) { if (sorted.Length == 0) return 0; var at = (sorted.Length - 1) * percentile; var lo = (int)at; var hi = (int)Math.Ceiling(at); return sorted[lo] + (sorted[hi] - sorted[lo]) * (at - lo); }
        return new(Percentile(.5), Percentile(.95));
    }
    public static IReadOnlyList<RetrievalSummaryDto> Summarize(IReadOnlyList<RetrievalMetricDto> results, int expectedCases)
    {
        double? Average(IEnumerable<double?> values) { var numbers = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray(); return numbers.Length == 0 ? null : numbers.Average(); }
        return Modes.Select(mode => {
            var all = results.Where(x => x.Mode == mode).ToArray(); var valid = all.Where(x => x.Unavailable is null).ToArray();
            return new RetrievalSummaryDto(mode, valid.Length == expectedCases && valid.Length == all.Length && valid.All(x => x.ActualMode == mode), valid.Length, all.Length - valid.Length,
                Average(valid.Select(x => x.Recall)), Average(valid.Select(x => x.ReciprocalRank)), Average(valid.Select(x => x.Ndcg)), Average(valid.Select(x => x.Refused is bool refused ? (double?)(refused ? 1 : 0) : null)),
                Latency(valid.Select(x => x.RewriteMs)), Latency(valid.Select(x => x.EmbedMs)), Latency(valid.Select(x => x.SearchMs)), Latency(valid.Select(x => x.RerankMs)), Latency(valid.Select(x => x.ElapsedMs)));
        }).ToArray();
    }
}
