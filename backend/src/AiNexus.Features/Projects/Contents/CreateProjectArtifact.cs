using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Artifacts.Management;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects.Contents;

/// <summary>在專案中建立成果，同時需要成果功能；專案權限由成果模組檢查。</summary>
internal sealed class CreateProjectArtifact(ArtifactService artifacts)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/artifacts", (Guid id, CreateArtifactRequest body, ICurrentUser user, CreateProjectArtifact handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.Artifacts);

    public Task<Result<ArtifactDto>> HandleAsync(Guid userId, Guid projectId, CreateArtifactRequest body, CancellationToken ct) =>
        artifacts.CreateAsync(userId, body with { ProjectId = projectId }, ct);
}
