using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

/// <summary>The 200 most recently updated artifacts the user may read, with whether each one is editable.</summary>
internal static class ListArtifacts
{
    private const int PageSize = 200;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, access, user.Id, ct)))
        .WithName("ListArtifacts").Produces<IReadOnlyList<ArtifactSummaryDto>>();

    public static async Task<IReadOnlyList<ArtifactSummaryDto>> HandleAsync(NexusDbContext db, ResourceAccess access, Guid actor, CancellationToken ct)
    {
        var query = await access.QueryAsync(actor, Artifact.Kind, ct);
        // Editable: owner, a named editor, or an editor of the live parent project.
        return await (from resource in query.AsNoTracking() join item in db.Set<Artifact>() on resource.Id equals item.Id orderby resource.UpdatedAt descending
            select new ArtifactSummaryDto(new(resource.Id, resource.Name, resource.Kind,
                resource.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == item.Id && m.UserId == actor && m.Role == "editor")
                    || db.Set<WorkspaceResource>().Any(p => p.Id == resource.ParentId && !p.IsDeleted && (p.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == p.Id && m.UserId == actor && m.Role == "editor"))),
                resource.OwnerId == actor, resource.UpdatedAt), item.Version, item.ProjectId)).Take(PageSize).ToListAsync(ct);
    }
}
