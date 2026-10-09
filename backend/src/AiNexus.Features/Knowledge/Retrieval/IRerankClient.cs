namespace AiNexus.Features.Knowledge.Retrieval;

public interface IRerankClient
{
    string Provider { get; }
    Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct);
}
