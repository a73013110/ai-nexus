namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed record RewriteTurn(string Role, string Text);

public interface IQueryRewriter
{
    Task<string> RewriteAsync(Guid actor, string query, IReadOnlyList<RewriteTurn> history, CancellationToken ct);
}
