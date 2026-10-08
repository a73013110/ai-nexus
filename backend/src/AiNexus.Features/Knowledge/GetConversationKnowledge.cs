using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>The collections selected for one of the user's own conversations. Also used by generation through <see cref="KnowledgeRetrieval"/>.</summary>
internal static class GetConversationKnowledge
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (Guid id, ICurrentUser user, NexusDbContext db, ConversationService conversations, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, conversations, user.Id, id, ct)))
        .WithName("ConversationKnowledge").Produces<KnowledgeSelectionDto>();

    public static async Task<KnowledgeSelectionDto> HandleAsync(NexusDbContext db, ConversationService conversations, Guid actor, Guid conversation, CancellationToken ct)
    {
        await conversations.OwnedAsync(actor, conversation, ct);
        return new(await db.Set<ConversationKnowledge>().Where(x => x.ConversationId == conversation).Select(x => x.CollectionId).ToListAsync(ct));
    }
}
