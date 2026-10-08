using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Artifacts;

/// <summary>The owner replaces an artifact's named members and group grants.</summary>
internal static class SaveArtifactAccess
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/access", async (Guid id, ResourceAclRequest request, ICurrentUser user, ResourceAccess access, CancellationToken ct) =>
        {
            await access.SetAclAsync(user.Id, id, Artifact.Kind, request, ct);
            return Results.NoContent();
        })
        .WithName("SaveArtifactAccess").Produces(204);
}
