using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed record AdminUserActivityDto(UsageTotalsDto Usage, int Conversations);
public sealed record AdminUserDto(Guid Id, string Account, string DisplayName, DateTimeOffset LastSeenAt, IReadOnlyList<string> RoleIds, AdminUserActivityDto? Activity = null, bool Enabled = true, UserAuthenticationDto? Authentication = null, AttachmentStorageDto? Storage = null);
public sealed record AdminUsersDto(IReadOnlyList<AdminUserDto> Users, int Total, int Offset);

/// <summary>Active users, 100 per page by account, with roles, usage, conversation count and storage.</summary>
internal sealed class ListAdminUsers(NexusDbContext db, UsageReports reports, AttachmentQuota quota)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users", async (string? search, int? offset, ListAdminUsers handler, CancellationToken ct) => (await handler.HandleAsync(search, offset ?? 0, ct)).ToHttpResult())
        .WithName("ListAdminUsers").Produces<AdminUsersDto>();

    public async Task<Result<AdminUsersDto>> HandleAsync(string? search, int offset, CancellationToken ct)
    {
        if (search?.Length > 120 || offset < 0) return AdministrationErrors.InvalidSearch;
        var query = db.Users.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Account.Contains(search) || x.DisplayName.Contains(search) || x.LocalAccount != null && x.LocalAccount.Contains(search) || x.AdAccount != null && x.AdAccount.Contains(search));
        var total = await query.CountAsync(ct); var rows = await query.OrderBy(x => x.Account).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray(); var roles = await db.Set<UserRole>().AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync(ct);
        var usage = (await reports.ByOwnersAsync(ids, ct)).ToDictionary(x => x.OwnerId, x => x.Usage);
        var conversations = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).Where(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId).Select(g => new { OwnerId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.OwnerId, x => x.Count, ct);
        var storage = await quota.ForOwnersAsync(ids, ct);
        return new AdminUsersDto(rows.Select(x => new AdminUserDto(x.Id, x.Account, x.DisplayName, x.LastSeenAt, roles.Where(y => y.UserId == x.Id).Select(y => y.RoleId).ToArray(),
            new(usage.GetValueOrDefault(x.Id) ?? new(0, 0, 0, 0, 0, 0, 0), conversations.GetValueOrDefault(x.Id)), x.Enabled, UserAccounts.Authentication(x), storage[x.Id])).ToArray(), total, offset);
    }
}
