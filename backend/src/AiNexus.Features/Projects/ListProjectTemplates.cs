using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects;

/// <summary>The templates of a project the user may read, by title.</summary>
internal sealed class ListProjectTemplates(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/templates", (Guid id, ICurrentUser user, ListProjectTemplates handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<Result<IReadOnlyList<ProjectTemplateDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        return await db.Set<ProjectTemplate>().Where(x => x.ProjectId == id).OrderBy(x => x.Title).Select(x => new ProjectTemplateDto(x.Id, x.Title, x.Content)).ToListAsync(ct);
    }
}
