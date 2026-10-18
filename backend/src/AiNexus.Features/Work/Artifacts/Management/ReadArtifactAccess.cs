using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Artifacts.Management;

/// <summary>擁有者讀取有哪些人可以檢視或編輯此成果。</summary>
internal sealed class ReadArtifactAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/access", (Guid id, ICurrentUser user, ReadArtifactAccess handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ArtifactAccess");

    public Task<Result<ResourceAclDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct) =>
        access.AclAsync(actor, id, Artifact.Kind, ct);
}
