using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Contents;

/// <summary>把上傳的檔案加為可編輯的有效專案的參考文件（最多 50 個）。</summary>
internal sealed class AddProjectFile(AppDbContext db, ResourceAccess access, DocumentService documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/files", async (Guid id, AddDocumentRequest body, ICurrentUser user, AddProjectFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.AttachmentId, ct)).ToHttpResult()).Audited(ProjectsAudit.FileCreated);

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, Guid attachment, CancellationToken ct)
    {
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        if (await db.ProjectFiles(id).CountAsync(ct) >= Project.MaxFiles) return ProjectsErrors.FileLimit;
        return await documents.AddAsync(actor, null, attachment, ct, id);
    }
}
