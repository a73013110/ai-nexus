using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>The owner reads who else may view or edit an artifact.</summary>
internal sealed class ReadArtifactAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/access", (Guid id, ICurrentUser user, ReadArtifactAccess handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ArtifactAccess");

    public Task<Result<ResourceAclDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => access.AclAsync(actor, id, Artifact.Kind, ct);
}
