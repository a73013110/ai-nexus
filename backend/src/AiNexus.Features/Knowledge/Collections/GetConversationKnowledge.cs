using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>The collections selected for one of the user's own conversations. Also used by generation through <see cref="KnowledgeRetrieval"/>.</summary>
internal sealed class GetConversationKnowledge(NexusDbContext db, ConversationService conversations)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", (Guid id, ICurrentUser user, GetConversationKnowledge handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ConversationKnowledge");

    public async Task<Result<KnowledgeSelectionDto>> HandleAsync(Guid actor, Guid conversation, CancellationToken ct)
    {
        var owned = await conversations.OwnedAsync(actor, conversation, ct);
        if (!owned.IsSuccess) return owned.Error;
        return new KnowledgeSelectionDto(await db.Set<ConversationKnowledge>().Where(x => x.ConversationId == conversation).Select(x => x.CollectionId).ToListAsync(ct));
    }
}
