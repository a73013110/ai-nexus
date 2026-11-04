using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Templates;

/// <summary>使用者可讀取的專案範本，依標題排序。</summary>
internal sealed class ListProjectTemplates(AppDbContext db, ResourceAccess access) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/templates", (Guid id, ICurrentUser user, ListProjectTemplates handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct)).Audited(ProjectsAudit.TemplateRead);

    public async Task<Result<ProjectTemplateDto[]>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        return await db.ProjectTemplates.Where(x => x.ProjectId == id).OrderBy(x => x.Title).Select(x => new ProjectTemplateDto(x.Id, x.Title, x.Content)).ToArrayAsync(ct);
    }
}
