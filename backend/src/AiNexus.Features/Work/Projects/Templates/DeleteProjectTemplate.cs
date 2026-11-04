using AiNexus.Data.App;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects.Templates;

/// <summary>刪除可編輯的有效專案中的範本；刪除不存在的範本也視為成功。</summary>
internal sealed class DeleteProjectTemplate(AppDbContext db, ResourceAccess access, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/templates/{key:guid}", (Guid id, Guid key, ICurrentUser user, DeleteProjectTemplate handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, key, ct)).Audited(ProjectsAudit.TemplateDeleted);

    public async Task<Result> HandleAsync(Guid actor, Guid id, Guid key, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        if (await db.ProjectTemplates.Where(x => x.ProjectId == id && x.Id == key).ExecuteDeleteAsync(ct) > 0)
        {
            audit.Describe(resource: key);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
