using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Administration;

public sealed class AdminBootstrap(NexusDbContext db, IOptions<AdministrationOptions> options)
{
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];
    // Called under the identity write gate. A durable marker prevents a revoked grant from reappearing on login.
    public async Task ApplyAsync(NexusUser user, CancellationToken ct)
    {
        if (!options.Value.BootstrapAdministrators.Contains(AccountName(user.Account), StringComparer.OrdinalIgnoreCase) ||
            await db.Set<AdministratorBootstrap>().AnyAsync(x => x.UserId == user.Id, ct)) return;
        if (!await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id && x.RoleId == AdministrationConfiguration.Role, ct))
            db.Set<UserRole>().Add(new() { UserId = user.Id, RoleId = AdministrationConfiguration.Role });
        db.Set<AdministratorBootstrap>().Add(new() { UserId = user.Id });
        db.AuditEvents.Add(new() { OwnerId = user.Id, ResourceId = user.Id, Action = "admin.bootstrap", Result = "granted_once" });
        await db.SaveChangesAsync(ct);
    }
}
