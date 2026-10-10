using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>
/// The module's contract for other modules (projects, source imports): create an artifact with every check of the
/// <c>CreateArtifact</c> endpoint.
/// </summary>
public sealed class ArtifactService
{
    private readonly CreateArtifact create;

    internal ArtifactService(CreateArtifact create) => this.create = create;

    public Task<Result<ArtifactDto>> CreateAsync(Guid actor, CreateArtifactRequest request, CancellationToken ct) => create.HandleAsync(actor, request, ct);
}
