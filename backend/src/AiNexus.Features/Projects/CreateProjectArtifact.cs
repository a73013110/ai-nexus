using AiNexus.Platform.Errors;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Projects;

/// <summary>Creates an artifact in a project; needs the artifacts feature as well. Project access is checked by the artifacts module.</summary>
internal sealed class CreateProjectArtifact(ArtifactService artifacts)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/artifacts", (Guid id, CreateArtifactRequest body, ICurrentUser user, CreateProjectArtifact handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.Artifacts);

    public Task<Result<ArtifactDto>> HandleAsync(Guid userId, Guid projectId, CreateArtifactRequest body, CancellationToken ct) =>
        artifacts.CreateAsync(userId, body with { ProjectId = projectId }, ct);
}
