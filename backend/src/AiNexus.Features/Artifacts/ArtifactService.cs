using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>
/// The module's contract for other modules (projects, source imports): create an artifact with every check of the
/// <c>CreateArtifact</c> endpoint. Failures are thrown as <see cref="ApiException"/> for callers that cannot return a result.
/// </summary>
public sealed class ArtifactService
{
    private readonly CreateArtifact create;

    internal ArtifactService(CreateArtifact create) => this.create = create;

    public async Task<ArtifactDto> CreateAsync(Guid actor, CreateArtifactRequest request, CancellationToken ct)
    {
        var result = await create.HandleAsync(actor, request, ct);
        if (result.IsSuccess) return result.Value;
        var status = Problems.Status(result.Error.Kind);
        throw new ApiException(status, result.Error.Code, PublicErrorCatalog.Message(result.Error.Code, status));
    }
}
