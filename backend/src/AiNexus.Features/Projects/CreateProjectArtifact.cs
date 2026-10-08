using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Projects;

/// <summary>Creates an artifact in a project; needs the artifacts feature as well. Project access is checked by the artifacts module.</summary>
internal static class CreateProjectArtifact
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/artifacts", async (Guid id, CreateArtifactRequest body, ICurrentUser user, ArtifactService artifacts, CancellationToken ct) =>
            Results.Ok(await artifacts.CreateAsync(user.Id, body with { ProjectId = id }, ct)))
        .RequireAuthorization(Policies.Artifacts).Produces<ArtifactDto>();
}
