using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Management;

/// <summary>使用者可讀取的最新 100 個專案，以及依 id 取得單一專案。</summary>
internal sealed class BrowseProjects(AppDbContext db, ResourceAccess access) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("", (ICurrentUser user, BrowseProjects handler, CancellationToken ct) =>
            handler.ListAsync(user.Id, ct)).Audited(ProjectsAudit.ProjectRead);

        routes.MapGet("/{id:guid}", (Guid id, ICurrentUser user, BrowseProjects handler, CancellationToken ct) =>
            handler.GetAsync(user.Id, id, ct)).Audited(ProjectsAudit.ProjectRead);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (await access.QueryAsync(actor, Project.Kind, ct)).AsNoTracking()
            .Join(db.Projects.AsNoTracking(), resource => resource.Id, project => project.Id, (resource, project) => new { resource, project })
            .OrderByDescending(x => x.resource.UpdatedAt).Take(100).ToListAsync(ct);
        var described = await access.DescribeAllAsync(actor, [.. rows.Select(x => x.resource)], ct);
        return [.. rows.Zip(described, (row, resource) => row.project.ToDto(resource))];
    }

    public Task<Result<ProjectDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.LoadProjectAsync(db, actor, id, ct);
}
