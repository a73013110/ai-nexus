using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Contents;

/// <summary>使用者可讀取的專案中，最多 50 個參考文件。</summary>
internal sealed class ListProjectFiles(AppDbContext db, ResourceAccess access, DocumentService documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/files", (Guid id, ICurrentUser user, ListProjectFiles handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<Result<IReadOnlyList<DocumentDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        var ids = await db.ProjectFiles(id).Select(x => x.Id).Take(Project.MaxFiles).ToListAsync(ct);
        return await documents.DetailsAsync(actor, ids, ct);
    }
}
