using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Features.Admin.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Conversations;

/// <summary>使用者未被軟刪除的對話上所有不重複的標籤，依名稱排序。</summary>
internal sealed class ListConversationLabels(AppDbContext db)
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
