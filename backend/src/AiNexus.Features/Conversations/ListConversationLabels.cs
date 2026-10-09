using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>The distinct labels on the user's conversations that are not deleted, in order.</summary>
internal static class ListConversationLabels
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/labels", async (ICurrentUser user, NexusDbContext db, CancellationToken ct) => Results.Ok(await HandleAsync(db, user.Id, ct)))
        .WithName("ListConversationLabels").Produces<IReadOnlyList<string>>();

    private static async Task<IReadOnlyList<string>> HandleAsync(NexusDbContext db, Guid owner, CancellationToken ct)
        => await (from label in db.Set<ConversationLabel>() join conversation in db.Set<Conversation>() on label.ConversationId equals conversation.Id where conversation.OwnerId == owner select label.Name).Distinct().OrderBy(x => x).ToListAsync(ct);
}
