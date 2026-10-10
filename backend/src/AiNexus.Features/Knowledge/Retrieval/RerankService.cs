using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class RerankService(IEnumerable<IRerankClient> clients, RetrievalInvocation invocations, IOptions<KnowledgeOptions> options)
{
    public async Task<IReadOnlyList<KnowledgeHitDto>> RerankAsync(Guid actor, string query, IReadOnlyList<KnowledgeHitDto> candidates, CancellationToken ct)
    {
        var settings = options.Value.Rerank;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var texts = candidates.Select(x => EmbeddingInput.Document(x.Title, x.HeadingPath, x.Text)).ToArray();
        var scores = await invocations.RunAsync(actor, "rerank", settings.Provider, settings.Model, async () =>
            (await clients.Single(x => x.Provider == settings.Provider).RerankAsync(query, texts, timeout.Token), (long?)null), ct);
        if (scores.Count != candidates.Count || scores.Select(x => x.Index).Distinct().Count() != scores.Count || scores.Any(x => x.Index < 0 || x.Index >= candidates.Count || !double.IsFinite(x.Score)))
            throw new ExternalServiceException(Error.Upstream("rerank_invalid"), "重排回應數量、索引或分數不正確。");
        return scores.Where(x => x.Score >= settings.MinScore).OrderByDescending(x => x.Score).ThenBy(x => x.Index)
            .Select(x => candidates[x.Index] with { Score = x.Score, RerankScore = x.Score }).ToArray();
    }
}
