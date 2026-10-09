namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class NoopQueryRewriter : IQueryRewriter
{
    public Task<string> RewriteAsync(Guid actor, string query, IReadOnlyList<RewriteTurn> history, CancellationToken ct) => Task.FromResult(query);
}
