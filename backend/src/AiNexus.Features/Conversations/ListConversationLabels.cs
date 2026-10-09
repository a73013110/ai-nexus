using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>The distinct labels on the user's conversations that are not deleted, in order.</summary>
internal sealed class ListConversationLabels(NexusDbContext db)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/labels", async (ICurrentUser user, ListConversationLabels handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListConversationLabels");

    public async Task<IReadOnlyList<string>> HandleAsync(Guid owner, CancellationToken ct)
        => await (from label in db.Set<ConversationLabel>() join conversation in db.Set<Conversation>() on label.ConversationId equals conversation.Id where conversation.OwnerId == owner select label.Name).Distinct().OrderBy(x => x).ToListAsync(ct);
}
