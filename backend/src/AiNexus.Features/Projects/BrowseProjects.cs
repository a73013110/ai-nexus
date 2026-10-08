using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>The newest 100 projects the user may read, and one project by id.</summary>
internal static class BrowseProjects
{
    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) => Results.Ok(await ListAsync(db, access, user.Id, ct)))
        .Produces<IReadOnlyList<ProjectDto>>();

    public static RouteHandlerBuilder MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) => Results.Ok(await access.LoadProjectAsync(db, user.Id, id, ct)))
        .Produces<ProjectDto>();

    private static async Task<IReadOnlyList<ProjectDto>> ListAsync(NexusDbContext db, ResourceAccess access, Guid actor, CancellationToken ct)
    {
        var resources = await (await access.QueryAsync(actor, Project.Kind, ct)).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct);
        var ids = resources.Select(x => x.Id).ToArray();
        var rows = await db.Set<Project>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var result = new List<ProjectDto>();
        foreach (var resource in resources) result.Add(rows[resource.Id].ToDto(await access.DescribeAsync(actor, resource, ct)));
        return result;
    }
}
