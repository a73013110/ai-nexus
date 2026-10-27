using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Templates;

/// <summary>使用者可讀取的專案範本，依標題排序。</summary>
internal sealed class ListProjectTemplates(AppDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/templates", (Guid id, ICurrentUser user, ListProjectTemplates handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync()).Audited(ProjectsAudit.TemplateRead);

    public async Task<Result<IReadOnlyList<ProjectTemplateDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        return await db.Set<ProjectTemplate>().Where(x => x.ProjectId == id).OrderBy(x => x.Title).Select(x => new ProjectTemplateDto(x.Id, x.Title, x.Content)).ToListAsync(ct);
    }
}
