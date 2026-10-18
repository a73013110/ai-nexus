using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects.Management;

/// <summary>使用者可讀取的最新 100 個專案，以及依 id 取得單一專案。</summary>
internal sealed class BrowseProjects(AppDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, BrowseProjects handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(user.Id, ct)));

    public static RouteHandlerBuilder MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, BrowseProjects handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (await access.QueryAsync(actor, Project.Kind, ct)).AsNoTracking()
            .Join(db.Set<Project>().AsNoTracking(), resource => resource.Id, project => project.Id, (resource, project) => new { resource, project })
            .OrderByDescending(x => x.resource.UpdatedAt).Take(100).ToListAsync(ct);
        var described = await access.DescribeAllAsync(actor, [.. rows.Select(x => x.resource)], ct);
        return [.. rows.Zip(described, (row, resource) => row.project.ToDto(resource))];
    }

    public Task<Result<ProjectDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.LoadProjectAsync(db, actor, id, ct);
}
