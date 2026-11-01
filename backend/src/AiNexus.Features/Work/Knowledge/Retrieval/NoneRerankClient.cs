namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed class NoneRerankClient : IRerankClient
{
    public Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct) => Task.FromResult<IReadOnlyList<RerankScore>>([]);
}
