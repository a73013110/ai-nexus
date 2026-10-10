using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects;

/// <summary>Up to 50 reference documents of a project the user may read.</summary>
internal sealed class ListProjectFiles(NexusDbContext db, ResourceAccess access, DocumentService documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/files", (Guid id, ICurrentUser user, ListProjectFiles handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<Result<IReadOnlyList<DocumentDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        var ids = await db.Set<WorkspaceResource>().Where(x => x.ParentId == id && x.Kind == "document").Select(x => x.Id).Take(Project.MaxFiles).ToListAsync(ct);
        return await documents.DetailsAsync(actor, ids, ct);
    }
}
