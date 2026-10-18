using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects.Contents;

/// <summary>把上傳的檔案加為可編輯的有效專案的參考文件（最多 50 個）。</summary>
internal sealed class AddProjectFile(AppDbContext db, ResourceAccess access, DocumentService documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/files", async (Guid id, AddDocumentRequest body, ICurrentUser user, AddProjectFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.AttachmentId, ct)).ToHttpResult());

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, Guid attachment, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        if (await db.Set<Resource>().CountAsync(x => x.ParentId == id && x.Kind == "document", ct) >= Project.MaxFiles) return ProjectsErrors.FileLimit;
        return await documents.AddAsync(actor, null, attachment, ct, id);
    }
}
