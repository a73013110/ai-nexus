using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Management;

/// <summary>使用者可讀取的專案（可只取進行中或已封存），以及依 id 取得單一專案。搜尋與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。</summary>
internal sealed class BrowseProjects(AppDbContext db, ResourceAccess access) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("", (bool? archived, [AsParameters] TableRequest table, ICurrentUser user, BrowseProjects handler, CancellationToken ct) =>
            handler.ListAsync(user.Id, archived, table, ct)).Audited(ProjectsAudit.ProjectRead);

        routes.MapGet("/{id:guid}", (Guid id, ICurrentUser user, BrowseProjects handler, CancellationToken ct) =>
            handler.GetAsync(user.Id, id, ct)).Audited(ProjectsAudit.ProjectRead);
    }

    public async Task<Result<TablePage<ProjectDto>>> ListAsync(Guid actor, bool? archived, TableRequest table, CancellationToken ct)
    {
        var query = (await access.QueryAsync(actor, Project.Kind, ct)).AsNoTracking()
            .Join(db.Projects.AsNoTracking(), resource => resource.Id, project => project.Id, (resource, project) => new Row { Resource = resource, Project = project });
        if (archived is { } value) query = query.Where(x => x.Project.IsArchived == value);
        var page = await query.ToTablePageAsync(table, Columns, x => x, ct);
        if (!page.IsSuccess) return page.Error;
        var described = await access.DescribeAllAsync(actor, [.. page.Value.Items.Select(x => x.Resource)], ct);
        return page.Value.With<ProjectDto>([.. page.Value.Items.Zip(described, (row, resource) => row.Project.ToDto(resource))]);
    }

    private sealed class Row
    {
        public required Resource Resource { get; init; }
        public required Project Project { get; init; }
    }

    private static readonly TableColumns<Row> Columns = new TableColumns<Row>()
        .Text("name", x => x.Resource.Name)
        .Range("updatedAt", x => x.Resource.UpdatedAt)
        .Key(x => x.Resource.Id)
        .DefaultSort("updatedAt", descending: true);

    public Task<Result<ProjectDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.LoadProjectAsync(db, actor, id, ct);
}
