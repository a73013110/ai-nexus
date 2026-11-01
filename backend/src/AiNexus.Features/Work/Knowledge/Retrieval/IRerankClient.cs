namespace AiNexus.Features.Work.Knowledge.Retrieval;

public interface IRerankClient
{
    Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct);
}
