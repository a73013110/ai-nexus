using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Billing;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Administration.Users;

public sealed record AdminUserDetailDto(AdminUserDto User, PersonalUsageDto Usage, IReadOnlyList<UsageKindDto> Kinds, int Conversations);

/// <summary>One user's account, roles, usage and storage. The read is audited.</summary>
internal sealed class GetAdminUserInsights(NexusDbContext db, AdministrativeReadAudit reads, UsageReports usage, PersonalUsage personal)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/insights", async (Guid id, ICurrentUser user, GetAdminUserInsights handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("GetAdminUserInsights");

    public async Task<Result<AdminUserDetailDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await reads.AllowedAsync(actor, ct)) return AdministrationErrors.AdminRequired;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return AdministrationErrors.NotFound;
        var roles = await db.Set<UserRole>().Where(x => x.UserId == id).Select(x => x.RoleId).ToListAsync(ct);
        var report = await personal.ForOwnerAsync(id, ct);
        var detail = new AdminUserDetailDto(new(id, user.Account, user.DisplayName, user.LastSeenAt, roles, null, user.Enabled, UserAccounts.Authentication(user), report.Storage), report, await usage.KindsAsync(id, ct), await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).CountAsync(x => x.OwnerId == id, ct));
        var audited = await reads.RecordAsync(actor, "admin.user_usage_read", id, new { userId = id }, ct);
        return audited.IsSuccess ? detail : audited.Error;
    }
}
