using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Artifacts.Management;

/// <summary>擁有者整批取代成果的具名成員與群組授權。</summary>
internal sealed class SaveArtifactAccess(ResourceAccess access) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/access", (
                Guid id, ResourceAclRequest request, ICurrentUser user, SaveArtifactAccess handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, request, ct))
        .Audited(ArtifactsAudit.ArtifactShared)
        .WithName("SaveArtifactAccess");

    public Task<Result> HandleAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) =>
        access.SetAclAsync(actor, id, Artifact.Kind, request, ct);
}
