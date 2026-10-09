namespace AiNexus.Features.Integrations;

public sealed record SourceActor(string Sid, string Account);

public sealed record SourceSearchRequest(string Query, string Kind = "all");

public interface IControlledSourceAdapter
{
    string Id { get; }
    Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct);
    Task<SourceDetailDto?> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct);
}
