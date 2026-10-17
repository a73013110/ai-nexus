using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts.Management;

/// <summary>擁有者整批取代成果的具名成員與群組授權。</summary>
internal sealed class SaveArtifactAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/access", (Guid id, ResourceAclRequest request, ICurrentUser user, SaveArtifactAccess handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, request, ct).ToHttpResultAsync())
        .WithName("SaveArtifactAccess");

    public Task<Result> HandleAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) => access.SetAclAsync(actor, id, Artifact.Kind, request, ct);
}
