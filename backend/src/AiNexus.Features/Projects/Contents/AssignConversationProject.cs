using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects.Contents;

/// <summary><c>ProjectId</c> 為 null 時會把對話移出專案。</summary>
[ValidatedInHandler("Every check needs the database: the project must be active and readable.")]
public sealed record ConversationProjectRequest(Guid? ProjectId);

/// <summary>
/// 把使用者自己的對話移入可讀取的有效專案，或移出專案。在對話的生成鎖下執行，避免與正在開始的回答互相競爭。
/// </summary>
internal sealed class AssignConversationProject(NexusDbContext db, ResourceAccess access, AccessService features, GenerationScheduler scheduler, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapPut("/conversations/{id:guid}/project", async (Guid id, ConversationProjectRequest body, ICurrentUser user, AssignConversationProject handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.ProjectId, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Chat);

    public async Task<Result<ConversationDto>> HandleAsync(Guid actor, Guid id, Guid? projectId, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            var conversation = await db.Conversations.Include(x => x.Labels).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
            if (conversation is null) return ProjectsErrors.ConversationMissing;
            if (await db.Runs.AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct)) return ProjectsErrors.GenerationActive;
            if (projectId is Guid project)
            {
                if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "projects")) return AccessControlErrors.FeatureForbidden;
                var active = await access.RequireActiveAsync(db, actor, project, write: false, ct);
                if (!active.IsSuccess) return active.Error;
            }
            conversation.ProjectId = projectId; conversation.UpdatedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "conversation.project", ResourceId = id, Result = projectId is null ? "removed" : "assigned" });
            await db.SaveChangesAsync(ct);
            return conversation.ToDto();
        }
        finally { conversationLock.Dispose(); }
    }
}
