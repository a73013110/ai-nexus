using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Operations;

public sealed record AdminUserDetailDto(AdminUserDto User, PersonalUsageDto Usage, IReadOnlyList<UsageKindDto> Kinds, int Conversations);

/// <summary>單一使用者的帳號、角色、用量與儲存空間；讀取會稽核。</summary>
internal sealed class GetAdminUserInsights(AppDbContext db, AdministrativeReadAudit reads, UsageReports usage, PersonalUsage personal)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/insights", async (Guid id, ICurrentUser user, GetAdminUserInsights handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("GetAdminUserInsights");

    public async Task<Result<AdminUserDetailDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await reads.AllowedAsync(actor, ct)) return AccessControlErrors.FeatureForbidden;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return OperationsErrors.AdminResourceNotFound;
        var roles = await db.Set<UserRole>().Where(x => x.UserId == id).Select(x => x.RoleId).ToListAsync(ct);
        var report = await personal.ForOwnerAsync(id, ct);
        var detail = new AdminUserDetailDto(
            new(id, user.Account, user.DisplayName, user.LastSeenAt, roles, null, user.Enabled, UserAccounts.Authentication(user), report.Storage, user.Email, user.EmailSource, user.EmailVerifiedAt),
            report,
            await usage.KindsAsync(id, ct),
            await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).CountAsync(x => x.OwnerId == id, ct));
        var audited = await reads.RecordAsync(actor, "admin.user_usage_read", id, new { userId = id }, ct);
        return audited.IsSuccess ? detail : audited.Error;
    }
}
