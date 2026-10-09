using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>Adds an uploaded file as a reference document of an active project the user may edit (at most 50).</summary>
internal sealed class AddProjectFile(NexusDbContext db, ResourceAccess access, DocumentService documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/files", async (Guid id, AddDocumentRequest body, ICurrentUser user, AddProjectFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.AttachmentId, ct)).ToHttpResult())
        .Produces<DocumentDto>();

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, Guid attachment, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        if (await db.Set<WorkspaceResource>().CountAsync(x => x.ParentId == id && x.Kind == "document", ct) >= Project.MaxFiles) return ProjectErrors.FileLimit;
        return await documents.AddAsync(actor, null, attachment, ct, id);
    }
}
