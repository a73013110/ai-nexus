using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Projects;

/// <summary>Who may read or edit a project, through the shared resource access list (owner only).</summary>
internal static class ShareProject
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/access", async (Guid id, ICurrentUser user, ResourceAccess access, CancellationToken ct) => Results.Ok(await access.AclAsync(user.Id, id, Project.Kind, ct)))
            .Produces<ResourceAclDto>();
        routes.MapPut("/{id:guid}/access", async (Guid id, ResourceAclRequest body, ICurrentUser user, ResourceAccess access, CancellationToken ct) =>
        {
            await access.SetAclAsync(user.Id, id, Project.Kind, body, ct);
            return Results.NoContent();
        });
    }
}
