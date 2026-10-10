using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects;

/// <summary>Who may read or edit a project, through the shared resource access list (owner only).</summary>
internal sealed class ShareProject(ResourceAccess access)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/access", (Guid id, ICurrentUser user, ShareProject handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync());
        routes.MapPut("/{id:guid}/access", (Guid id, ResourceAclRequest body, ICurrentUser user, ShareProject handler, CancellationToken ct) => handler.SetAsync(user.Id, id, body, ct).ToHttpResultAsync());
    }

    public Task<Result<ResourceAclDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.AclAsync(actor, id, Project.Kind, ct);

    public Task<Result> SetAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) => access.SetAclAsync(actor, id, Project.Kind, request, ct);
}
