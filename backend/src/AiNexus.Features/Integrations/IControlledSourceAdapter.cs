using AiNexus.Platform.Errors;

namespace AiNexus.Features.Integrations;

public sealed record SourceActor(string Sid, string Account);

public sealed record SourceSearchRequest(string Query, string Kind = "all");

public interface IControlledSourceAdapter
{
    string Id { get; }
    Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct);
    /// <summary>The record, or <c>source_record_missing</c> when the actor may not read it (or it does not exist).</summary>
    Task<Result<SourceDetailDto>> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct);
}
