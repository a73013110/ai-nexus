using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

public sealed class AdminBootstrap(NexusDbContext db, AccessService access, IOptions<AdministrationOptions> options)
{
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];
    private bool Applies(string account) => options.Value.BootstrapAdministrators.Contains(AccountName(account), StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <see cref="ApplyAsync"/> would grant anything, so sign-in must take the identity write gate.</summary>
    public async Task<bool> PendingAsync(NexusUser user, CancellationToken ct)
        => Applies(user.Account) && !await db.Set<AdministratorBootstrap>().AnyAsync(x => x.UserId == user.Id, ct);
    // Called under the identity write gate. A durable marker prevents a revoked grant from reappearing on login.
    public async Task ApplyAsync(NexusUser user, CancellationToken ct)
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
