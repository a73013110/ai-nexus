using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>讀取使用者自己對話所選定的知識庫；生成流程也會透過 <see cref="KnowledgeRetrieval"/> 使用同一份選取。</summary>
internal sealed class GetConversationKnowledge(AppDbContext db, ConversationService conversations)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", (Guid id, ICurrentUser user, GetConversationKnowledge handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ConversationKnowledge");

    public async Task<Result<KnowledgeSelectionDto>> HandleAsync(Guid actor, Guid conversation, CancellationToken ct)
    {
        var owned = await conversations.RequireOwnedAsync(actor, conversation, ct);
        if (!owned.IsSuccess) return owned.Error;
        return new KnowledgeSelectionDto(await db.Set<ConversationCollection>().Where(x => x.ConversationId == conversation).Select(x => x.CollectionId).ToListAsync(ct));
    }
}
