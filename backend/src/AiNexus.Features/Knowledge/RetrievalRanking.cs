namespace AiNexus.Features.Knowledge;

public static class RetrievalRanking
{
    public static IReadOnlyList<KnowledgeHitDto> Fuse(IReadOnlyList<KnowledgeHitDto> vector, IReadOnlyList<KnowledgeHitDto> keyword, KnowledgeOptions options)
    {
        var hits = new Dictionary<Guid, KnowledgeHitDto>();
        foreach (var (hit, index) in vector.Select((x, i) => (x, i))) hits[hit.ChunkId] = hit with { VectorRank = index + 1, RrfScore = options.VectorWeight / (options.RrfK + index + 1) };
        foreach (var (hit, index) in keyword.Select((x, i) => (x, i)))
        {
            var previous = hits.GetValueOrDefault(hit.ChunkId) ?? hit with { RrfScore = 0 };
            hits[hit.ChunkId] = previous with { FtsRank = index + 1, RrfScore = previous.RrfScore + options.FtsWeight / (options.RrfK + index + 1) };
        }
        return hits.Values.OrderByDescending(x => x.RrfScore).ThenBy(x => x.ChunkId).Take(options.RerankCandidates).Select(x => x with { Score = x.RrfScore ?? 0 }).ToArray();
    }
    public static IReadOnlyList<KnowledgeHitDto> Context(IReadOnlyList<KnowledgeHitDto> ranked, KnowledgeOptions options)
    {
        var counts = new Dictionary<Guid, int>(); var selected = new List<KnowledgeHitDto>();
        foreach (var hit in ranked.DistinctBy(x => x.ChunkId))
        {
            var count = counts.GetValueOrDefault(hit.DocumentId);
            if (count >= options.MaxChunksPerDocument) continue;
            counts[hit.DocumentId] = count + 1; selected.Add(hit);
            if (selected.Count == options.TopK) break;
        }
        // Keep relevance order between groups; within adjacent groups restore document order.
        var merged = new List<KnowledgeHitDto>(); var consumed = new HashSet<Guid>();
        foreach (var anchor in selected)
        {
            if (consumed.Contains(anchor.ChunkId)) continue;
            var group = selected.Where(x => x.DocumentId == anchor.DocumentId).OrderBy(x => x.Ordinal).ToList();
            var start = group.FindIndex(x => x.ChunkId == anchor.ChunkId); var end = start;
            while (start > 0 && group[start - 1].Ordinal + 1 == group[start].Ordinal) start--;
            while (end + 1 < group.Count && group[end].Ordinal + 1 == group[end + 1].Ordinal) end++;
            var text = group[start].Text;
            for (var i = start + 1; i <= end; i++) text = Join(text, group[i].Text);
            for (var i = start; i <= end; i++) consumed.Add(group[i].ChunkId);
            merged.Add(anchor with { Text = text, PageNumber = group[start].PageNumber, EndPage = group[end].EndPage, Ordinal = group[start].Ordinal });
        }
        var remaining = options.ContextTokens; var result = new List<KnowledgeHitDto>();
        foreach (var hit in merged)
        {
            var framing = TokenEstimator.Estimate(hit.Title + hit.HeadingPath) + 32;
            if (remaining <= framing) break;
            var text = TokenEstimator.Truncate(hit.Text, remaining - framing);
            remaining -= TokenEstimator.Estimate(text) + framing;
            if (text.Length > 0) result.Add(hit with { Text = text });
        }
        return result;
    }
    private static string Join(string left, string right)
    {
        // Only remove overlap at a complete boundary, never arbitrary matching words.
        for (var length = Math.Min(2000, Math.Min(left.Length, right.Length)); length > 0; length--)
            if (left.AsSpan(left.Length - length).SequenceEqual(right.AsSpan(0, length)) && (length == right.Length || right[length - 1] is '。' or '！' or '？' or ';' or '；' or '.' or '!' or '?' or '\n'))
                return left + right[length..];
        return left + "\n" + right;
    }
}
