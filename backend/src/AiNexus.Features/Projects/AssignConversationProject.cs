using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>A null <c>ProjectId</c> removes the conversation from its project.</summary>
public sealed record ConversationProjectRequest(Guid? ProjectId);

/// <summary>
/// Moves one of the user's own conversations into an active project they may read, or out of its project. Runs under the
/// generation state gate so it cannot race a starting answer.
/// </summary>
internal sealed class AssignConversationProject(NexusDbContext db, ResourceAccess access, AccessService features, GenerationScheduler scheduler, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapPut("/conversations/{id:guid}/project", async (Guid id, ConversationProjectRequest body, ICurrentUser user, AssignConversationProject handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.ProjectId, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Chat).Produces<ConversationDto>();

    public async Task<Result<ConversationDto>> HandleAsync(Guid actor, Guid id, Guid? projectId, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            var conversation = await db.Conversations.Include(x => x.Labels).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
            if (conversation is null) return ProjectErrors.ConversationMissing;
            if (await db.Runs.AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct)) return ProjectErrors.GenerationActive;
            if (projectId is Guid project)
            {
                if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "projects")) return ProjectErrors.AccessRequired;
                var active = await access.RequireActiveAsync(db, actor, project, write: false, ct);
                if (!active.IsSuccess) return active.Error;
            }
            conversation.ProjectId = projectId; conversation.UpdatedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "conversation.project", ResourceId = id, Result = projectId is null ? "removed" : "assigned" });
            await db.SaveChangesAsync(ct);
            return conversation.ToDto();
        }
        finally { scheduler.StateGate.Release(); }
    }
}
