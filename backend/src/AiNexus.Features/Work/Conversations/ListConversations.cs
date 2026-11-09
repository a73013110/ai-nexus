using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Conversations;

/// <summary>
/// 使用者對話列表，每頁 100 筆。依檢視（active、archived、favorites、all）篩選，可再依標籤，以及標題或任一訊息內文搜尋。
/// 收藏優先，其次依更新時間由新到舊。
/// </summary>
internal sealed class ListConversations(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (
                string? search, int? offset, string? view, string? label, ICurrentUser user, ListConversations handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, search, offset ?? 0, view ?? "active", label, ct))
        .NotAudited("側欄的自己對話清單")
        .WithName("ListConversations");

    public async Task<Result<ConversationDto[]>> HandleAsync(
        Guid owner, string? search, int offset, string view, string? label, CancellationToken ct)
    {
        var invalid = search?.Length > 120
            || label?.Length > 24
            || view is not ("active" or "archived" or "favorites" or "all")
            || offset < 0
            || offset > 100000;
        if (invalid) return ConversationsErrors.InvalidQuery;

        var query = db.Conversations.Include(x => x.Labels).AsNoTracking().Where(x => x.OwnerId == owner);
        if (view != "all") query = query.Where(x => x.IsArchived == (view == "archived"));
        if (view == "favorites") query = query.Where(x => x.IsFavorite);
        if (!string.IsNullOrWhiteSpace(label)) query = query.Where(x => x.Labels.Any(l => l.Name == label));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Matching(db, search);
        var rows = await query
            .OrderByDescending(x => x.IsFavorite)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Id)
            .Skip(offset)
            .Take(100)
            .ToListAsync(ct);
        return rows.Select(x => x.ToDto()).ToArray();
    }
}
