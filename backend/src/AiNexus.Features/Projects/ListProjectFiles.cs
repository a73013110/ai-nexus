using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects;

/// <summary>Up to 50 reference documents of a project the user may read.</summary>
internal static class ListProjectFiles
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/files", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, DocumentService documents, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, access, documents, user.Id, id, ct)))
        .Produces<IReadOnlyList<DocumentDto>>();

    private static async Task<IReadOnlyList<DocumentDto>> HandleAsync(NexusDbContext db, ResourceAccess access, DocumentService documents, Guid actor, Guid id, CancellationToken ct)
    {
        (await access.RequireAsync(actor, id, Project.Kind, ct)).OrThrow();
        var ids = await db.Set<WorkspaceResource>().Where(x => x.ParentId == id && x.Kind == "document").Select(x => x.Id).Take(Project.MaxFiles).ToListAsync(ct);
        return await documents.DetailsAsync(actor, ids, ct);
    }
}
