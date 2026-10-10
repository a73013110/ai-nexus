using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>The newest 100 projects the user may read, and one project by id.</summary>
internal sealed class BrowseProjects(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, BrowseProjects handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(user.Id, ct)));

    public static RouteHandlerBuilder MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, BrowseProjects handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var resources = await (await access.QueryAsync(actor, Project.Kind, ct)).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct);
        var ids = resources.Select(x => x.Id).ToArray();
        var rows = await db.Set<Project>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return (await access.DescribeAllAsync(actor, resources, ct)).Select(resource => rows[resource.Id].ToDto(resource)).ToArray();
    }

    public Task<Result<ProjectDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.LoadProjectAsync(db, actor, id, ct);
}
