using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>
/// A page of 100 of the user's conversations for one view (active, archived, favorites or all), optionally filtered by
/// label and by text in the title or any message. Favorites first, then the most recently updated.
/// </summary>
internal static class ListConversations
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (string? search, int? offset, string? view, string? label, ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
            (await HandleAsync(db, user.Id, search, offset ?? 0, view ?? "active", label, ct)).ToHttpResult())
        .WithName("ListConversations").Produces<IReadOnlyList<ConversationDto>>();

    private static async Task<Result<IReadOnlyList<ConversationDto>>> HandleAsync(NexusDbContext db, Guid owner, string? search, int offset, string view, string? label, CancellationToken ct)
    {
        if (search?.Length > 120 || label?.Length > 24 || view is not ("active" or "archived" or "favorites" or "all") || offset < 0 || offset > 100000) return ConversationsErrors.InvalidQuery;
        var query = db.Set<Conversation>().Include(x => x.Labels).AsNoTracking().Where(x => x.OwnerId == owner);
        if (view != "all") query = query.Where(x => x.IsArchived == (view == "archived"));
        if (view == "favorites") query = query.Where(x => x.IsFavorite);
        if (!string.IsNullOrWhiteSpace(label)) query = query.Where(x => x.Labels.Any(l => l.Name == label));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search.Trim()) || db.Set<Message>().Any(m => m.ConversationId == x.Id && m.Content.Contains(search.Trim())));
        var rows = await query.OrderByDescending(x => x.IsFavorite).ThenByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        return Result<IReadOnlyList<ConversationDto>>.Ok(rows.Select(x => x.ToDto()).ToList());
    }
}
