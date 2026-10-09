using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Users;

public sealed record AdminConversationDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsArchived, bool IsDeleted, int Messages);

public sealed record AdminConversationPageDto(IReadOnlyList<AdminConversationDto> Items, int Total, int Offset);

/// <summary>A user's conversations, 50 per page, optionally with deleted ones. The read is audited without the search text.</summary>
internal sealed class ListAdminUserConversations(NexusDbContext db, AdministrativeReadAudit reads)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/conversations", async (Guid id, string? search, int? offset, bool? includeDeleted, ICurrentUser user, ListAdminUserConversations handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, search, offset ?? 0, includeDeleted ?? false, ct)).ToHttpResult())
        .WithName("ListAdminUserConversations").Produces<AdminConversationPageDto>();

    public async Task<Result<AdminConversationPageDto>> HandleAsync(Guid actor, Guid owner, string? search, int offset, bool includeDeleted, CancellationToken ct)
    {
        if (!await reads.AllowedAsync(actor, ct)) return AdministrationErrors.AdminRequired;
        if (!AdministrativeReadAudit.ValidPage(search, offset)) return AdministrationErrors.InvalidSearch;
        if (!await db.Users.AnyAsync(x => x.Id == owner, ct)) return AdministrationErrors.NotFound;
        var query = db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking().Where(x => x.OwnerId == owner && (includeDeleted || !x.IsDeleted));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search) || db.Messages.Any(m => m.ConversationId == x.Id && m.Content.Contains(search)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(offset).Take(50)
            .Select(x => new AdminConversationDto(x.Id, x.Title, x.CreatedAt, x.UpdatedAt, x.IsArchived, x.IsDeleted, db.Messages.Count(m => m.ConversationId == x.Id))).ToListAsync(ct);
        var audited = await reads.RecordAsync(actor, "admin.conversations_list", owner, new { userId = owner, offset, includeDeleted, searchApplied = !string.IsNullOrWhiteSpace(search), count = rows.Count }, ct);
        return audited.IsSuccess ? new AdminConversationPageDto(rows, total, offset) : audited.Error;
    }
}
