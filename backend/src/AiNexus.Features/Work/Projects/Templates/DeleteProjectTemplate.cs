using AiNexus.Data.App;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Templates;

/// <summary>刪除可編輯的有效專案中的範本；刪除不存在的範本也視為成功。</summary>
internal sealed class DeleteProjectTemplate(AppDbContext db, ResourceAccess access, AuditTrail audit)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/templates/{key:guid}", async (Guid id, Guid key, ICurrentUser user, DeleteProjectTemplate handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, key, ct)).ToHttpResult()).Audited(ProjectsAudit.TemplateDeleted);

    public async Task<Result> HandleAsync(Guid actor, Guid id, Guid key, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        if (await db.Set<ProjectTemplate>().Where(x => x.ProjectId == id && x.Id == key).ExecuteDeleteAsync(ct) > 0)
        {
            audit.Describe(resource: key);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
