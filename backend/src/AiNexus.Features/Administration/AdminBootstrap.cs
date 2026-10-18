using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Administration;
using AiNexus.Data.App.Identity;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

/// <summary>依 <see cref="AdministrationOptions.BootstrapAdministrators"/> 所列的帳號，一次性授予管理者角色。</summary>
public sealed class AdminBootstrap(AppDbContext db, AccessService access, IOptions<AdministrationOptions> options) : ISignInGrant
{
    private bool Applies(string account) => options.Value.BootstrapAdministrators.Contains(UserAccounts.AccountName(account), StringComparer.OrdinalIgnoreCase);

    public async Task<bool> PendingAsync(User user, CancellationToken ct)
        => Applies(user.Account) && !await db.Set<AdministratorBootstrap>().AnyAsync(x => x.UserId == user.Id, ct);
    // 永久標記（AdministratorBootstrap）確保管理員權限被撤銷後，下次登入不會再被自動補回。
    public async Task ApplyAsync(User user, CancellationToken ct)
    {
        if (!Applies(user.Account) ||
            await db.Set<AdministratorBootstrap>().AnyAsync(x => x.UserId == user.Id, ct)) return;
        if (!await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id && x.RoleId == AdministrationConfiguration.Role, ct))
            db.Set<UserRole>().Add(new() { UserId = user.Id, RoleId = AdministrationConfiguration.Role });
        db.Set<AdministratorBootstrap>().Add(new() { UserId = user.Id });
        db.AuditEvents.Add(new() { OwnerId = user.Id, ResourceId = user.Id, Action = "admin.bootstrap", Result = "granted_once" });
        await db.SaveChangesAsync(ct);
        access.Invalidate();
    }
}
