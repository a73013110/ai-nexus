using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>
/// 使用者對話列表，每頁 100 筆。依檢視（active、archived、favorites、all）篩選，可再依標籤，以及標題或任一訊息內文搜尋。
/// 收藏優先，其次依更新時間由新到舊。
/// </summary>
internal sealed class ListConversations(NexusDbContext db)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", (string? search, int? offset, string? view, string? label, ICurrentUser user, ListConversations handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, search, offset ?? 0, view ?? "active", label, ct).ToHttpResultAsync())
        .WithName("ListConversations");

    public async Task<Result<IReadOnlyList<ConversationDto>>> HandleAsync(Guid owner, string? search, int offset, string view, string? label, CancellationToken ct)
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
