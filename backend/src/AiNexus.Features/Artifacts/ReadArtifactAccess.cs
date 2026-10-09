using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>The owner reads who else may view or edit an artifact.</summary>
internal static class ReadArtifactAccess
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/access", async (Guid id, ICurrentUser user, ResourceAccess access, CancellationToken ct) =>
            Results.Ok((await access.AclAsync(user.Id, id, Artifact.Kind, ct)).OrThrow()))
        .WithName("ArtifactAccess").Produces<ResourceAclDto>();
}
