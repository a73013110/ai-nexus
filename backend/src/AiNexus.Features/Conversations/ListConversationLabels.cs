using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>使用者未被軟刪除的對話上所有不重複的標籤，依名稱排序。</summary>
internal sealed class ListConversationLabels(NexusDbContext db)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/labels", async (ICurrentUser user, ListConversationLabels handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListConversationLabels");

    public async Task<IReadOnlyList<string>> HandleAsync(Guid owner, CancellationToken ct) => await (
            from label in db.Set<ConversationLabel>()
            join conversation in db.Set<Conversation>() on label.ConversationId equals conversation.Id
            where conversation.OwnerId == owner
            select label.Name)
        .Distinct()
        .OrderBy(x => x)
        .ToListAsync(ct);
}
