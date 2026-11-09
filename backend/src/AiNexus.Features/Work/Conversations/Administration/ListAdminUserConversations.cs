using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Conversations.Administration;

public sealed record AdminConversationDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsArchived, bool IsDeleted, int Messages);

public sealed record AdminConversationPageDto(IReadOnlyList<AdminConversationDto> Items, int Total, int Offset);

/// <summary>
/// 某使用者的對話列表，每頁 50 筆，可選擇含已刪除。稽核的資料主體是該使用者，搜尋文字只記「有填」。
/// 從使用者頁發起，handler 再檢一次 <see cref="FeatureCatalog.IdentityUsers"/>，不放寬對話擁有者檢查。
/// </summary>
internal sealed class ListAdminUserConversations(AppDbContext db, AccessService access, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/conversations", (Guid id, string? search, int? offset, bool? includeDeleted, ICurrentUser user, ListAdminUserConversations handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, search, offset ?? 0, includeDeleted ?? false, ct))
        .Audited(ConversationsAudit.UserConversationRead)
        .WithName("ListAdminUserConversations");

    public async Task<Result<AdminConversationPageDto>> HandleAsync(Guid actor, Guid owner, string? search, int offset, bool includeDeleted, CancellationToken ct)
    {
        if (!await access.HoldsAsync(actor, FeatureCatalog.IdentityUsers, ct)) return AccessControlErrors.FeatureForbidden;
        if (!ValidPage(search, offset)) return ConversationsErrors.InvalidSearch;
        if (!await db.Users.AnyAsync(x => x.Id == owner, ct)) return ConversationsErrors.AdminResourceNotFound;
        var query = db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking().Where(x => x.OwnerId == owner && (includeDeleted || !x.IsDeleted));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Matching(db, search);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(offset).Take(50)
            .Select(x => new AdminConversationDto(x.Id, x.Title, x.CreatedAt, x.UpdatedAt, x.IsArchived, x.IsDeleted, db.Messages.Count(m => m.ConversationId == x.Id))).ToListAsync(ct);
        audit.Describe(details: new { count = rows.Count });
        return new AdminConversationPageDto(rows, total, offset);
    }

    internal static bool ValidPage(string? search, int offset) => !(search?.Length > 120 || offset < 0);
}
