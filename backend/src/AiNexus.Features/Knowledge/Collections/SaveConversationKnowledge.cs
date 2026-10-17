using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>
/// 取代使用者自己的閒置對話所用的知識庫（最多三個，且須為使用者可讀取者）。
/// 在對話的生成鎖內執行，避免正在啟動的回答讀到修改到一半的選取。
/// </summary>
internal sealed class SaveConversationKnowledge(NexusDbContext db, ConversationService conversations, RetrievalAuthorization authorization, GenerationScheduler scheduler)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("", async (Guid id, KnowledgeSelectionDto request, ICurrentUser user, SaveConversationKnowledge handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, request, ct)).ToHttpResult())
        .WithName("SaveConversationKnowledge");

    public async Task<Result> HandleAsync(Guid actor, Guid conversation, KnowledgeSelectionDto request, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(conversation, ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var owned = await conversations.RequireOwnedAsync(actor, conversation, ct);
            if (!owned.IsSuccess) return owned.Error;
            var allowed = await authorization.CollectionsAsync(actor, request.CollectionIds, ct);
            if (!allowed.IsSuccess) return allowed.Error;
            if (await db.Runs.AnyAsync(x => x.ConversationId == conversation && x.ActiveOwnerId != null, ct)) return KnowledgeErrors.GenerationActive;
            var existing = await db.Set<ConversationKnowledge>().Where(x => x.ConversationId == conversation).ToListAsync(ct);
            db.RemoveRange(existing.Where(x => !request.CollectionIds.Contains(x.CollectionId)));
            db.AddRange(request.CollectionIds.Except(existing.Select(x => x.CollectionId)).Select(x => new ConversationKnowledge { ConversationId = conversation, CollectionId = x }));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Result.Success;
        }
        finally { conversationLock.Dispose(); }
    }
}
