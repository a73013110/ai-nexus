using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>Deletes a template of an active project the user may edit; deleting a missing template succeeds.</summary>
internal sealed class DeleteProjectTemplate(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/templates/{key:guid}", async (Guid id, Guid key, ICurrentUser user, DeleteProjectTemplate handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, key, ct)).ToHttpResult());

    public async Task<Result> HandleAsync(Guid actor, Guid id, Guid key, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var active = await access.RequireActiveAsync(db, actor, id, write: true, ct);
        if (!active.IsSuccess) return active.Error;
        if (await db.Set<ProjectTemplate>().Where(x => x.ProjectId == id && x.Id == key).ExecuteDeleteAsync(ct) > 0)
        {
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = key, Action = "project.template.deleted", Result = "deleted" });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
