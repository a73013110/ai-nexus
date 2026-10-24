using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Platform.Accounts;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Management;

/// <summary>
/// 帳號停用的人原本持有、現在已沒有任何啟用中的人持有的啟用角色（ADR 0019），例如唯一的管理員或人資：角色指派保留不動（復職即恢復），
/// 但停用後沒有人能做這個角色的事。指派給其他人，或確定不再需要時停用角色，項目就會消失。
/// </summary>
internal sealed class RoleHolderAccountReview(AppDbContext db, IActiveUsers activeUsers) : IAccountReviewSource
{
    public const string Orphaned = "access-control.role_orphaned";

    public async Task<AccountReviewReading> ReadAsync(CancellationToken ct)
    {
        var active = activeUsers.Ids;
        var assignments = db.Set<UserRole>();
        var rows = await (from assignment in assignments
                          join role in db.Set<Role>() on assignment.RoleId equals role.Id
                          where role.Enabled && !active.Contains(assignment.UserId)
                                && !assignments.Any(x => x.RoleId == role.Id && active.Contains(x.UserId))
                          select new { assignment.UserId, role.Name }).AsNoTracking().ToListAsync(ct);
        return new([.. rows.GroupBy(x => x.UserId).Select(x => new AccountReviewItem(x.Key, Orphaned, "帳號已停用，角色已沒有啟用中的持有人",
            string.Join("、", x.Select(y => y.Name).Order(StringComparer.CurrentCulture)) + "。", FeatureCatalog.AccessControlRoles))]);
    }
}
