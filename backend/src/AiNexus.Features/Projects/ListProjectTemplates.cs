using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>The templates of a project the user may read, by title.</summary>
internal static class ListProjectTemplates
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/templates", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, access, user.Id, id, ct)))
        .Produces<IReadOnlyList<ProjectTemplateDto>>();

    private static async Task<IReadOnlyList<ProjectTemplateDto>> HandleAsync(NexusDbContext db, ResourceAccess access, Guid actor, Guid id, CancellationToken ct)
    {
        await access.RequireAsync(actor, id, Project.Kind, ct);
        return await db.Set<ProjectTemplate>().Where(x => x.ProjectId == id).OrderBy(x => x.Title).Select(x => new ProjectTemplateDto(x.Id, x.Title, x.Content)).ToListAsync(ct);
    }
}
