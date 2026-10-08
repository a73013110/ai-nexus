using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// Replaces the collections (at most three the user may read) used by one of the user's own idle conversations. Runs
/// under the generation state gate so a starting answer never sees a half-changed selection.
/// </summary>
internal sealed class SaveConversationKnowledge(NexusDbContext db, ConversationService conversations, RetrievalAuthorization authorization, GenerationScheduler scheduler)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("", async (Guid id, KnowledgeSelectionDto request, ICurrentUser user, SaveConversationKnowledge handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, request, ct)).ToHttpResult())
        .WithName("SaveConversationKnowledge").Produces(204);

    public async Task<Result> HandleAsync(Guid actor, Guid conversation, KnowledgeSelectionDto request, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            await conversations.OwnedAsync(actor, conversation, ct);
            await authorization.CollectionsAsync(actor, request.CollectionIds, ct);
            if (await db.Runs.AnyAsync(x => x.ConversationId == conversation && x.ActiveOwnerId != null, ct)) return KnowledgeErrors.GenerationActive;
            var existing = await db.Set<ConversationKnowledge>().Where(x => x.ConversationId == conversation).ToListAsync(ct);
            db.RemoveRange(existing.Where(x => !request.CollectionIds.Contains(x.CollectionId)));
            db.AddRange(request.CollectionIds.Except(existing.Select(x => x.CollectionId)).Select(x => new ConversationKnowledge { ConversationId = conversation, CollectionId = x }));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Result.Success;
        }
        finally { scheduler.StateGate.Release(); }
    }
}
