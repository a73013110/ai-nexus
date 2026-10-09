using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Administration;

/// <summary>Grants the administrator role once to the accounts listed in <see cref="AdministrationOptions.BootstrapAdministrators"/>.</summary>
public sealed class AdminBootstrap(NexusDbContext db, AccessService access, IOptions<AdministrationOptions> options) : ISignInGrant
{
    private bool Applies(string account) => options.Value.BootstrapAdministrators.Contains(UserAccounts.AccountName(account), StringComparer.OrdinalIgnoreCase);

    public async Task<bool> PendingAsync(NexusUser user, CancellationToken ct)
        => Applies(user.Account) && !await db.Set<AdministratorBootstrap>().AnyAsync(x => x.UserId == user.Id, ct);
    // A durable marker prevents a revoked grant from reappearing on login.
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
