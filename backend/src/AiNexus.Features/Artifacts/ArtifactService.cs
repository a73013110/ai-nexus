using AiNexus.Features.Artifacts.Management;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>
/// 提供給其他模組（專案、來源匯入）的建立介面，與 <c>CreateArtifact</c> 端點套用相同的所有檢查。
/// </summary>
public sealed class ArtifactService
{
    private readonly CreateArtifact create;

    internal ArtifactService(CreateArtifact create) => this.create = create;

    public Task<Result<ArtifactDto>> CreateAsync(Guid actor, CreateArtifactRequest request, CancellationToken ct) => create.HandleAsync(actor, request, ct);
}
