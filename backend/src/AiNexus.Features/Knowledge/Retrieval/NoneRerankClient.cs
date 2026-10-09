namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class NoneRerankClient : IRerankClient
{
    public string Provider => "none";
    public Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct) => Task.FromResult<IReadOnlyList<RerankScore>>([]);
}
