using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity;

public sealed class CurrentUser(NexusDbContext db, IHttpContextAccessor accessor, StorageReadiness storage, IdentityWriteLock writeLock, IEnumerable<ISignInGrant> grants,
    DisplayNameCache names, TimeProvider clock) : IRequestUser, ICurrentUser
{
    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(5);
    private NexusUser? resolved;
    public Guid? ResolvedId => resolved?.Id;
    Guid ICurrentUser.Id => ((ICurrentUser)this).User.Id;
    NexusUser ICurrentUser.User => resolved ?? throw new InvalidOperationException("ICurrentUser is resolved by the /api/v1 endpoint filter; declare it as a handler parameter.");

    public async Task<Result<NexusUser>> GetAsync(CancellationToken ct = default)
    {
        if (resolved is not null) return resolved;
        storage.RequireConfigured();
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return IdentityErrors.AuthenticationRequired;
        if (Guid.TryParse(principal.FindFirstValue(SessionIdentity.UserId), out var userId))
        {
            // Cookie validation has just read this row; track that copy instead of reading it again.
            var linked = Track(SessionIdentity.ValidatedUser(accessor.HttpContext!, userId)) ?? await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
            var testing = principal.HasClaim(x => x.Type == SessionIdentity.ActorId);
            if (linked is null || !SessionIdentity.Available(linked) || !SessionIdentity.MatchesVersion(linked, principal.FindFirstValue(SessionIdentity.Version)) ||
                !testing && !SessionIdentity.Allows(linked, principal.FindFirstValue(SessionIdentity.Method)))
                return IdentityErrors.SessionRevoked;
            return resolved = linked;
        }
        var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
        if (sid is null && OperatingSystem.IsWindows() && principal.Identity is WindowsIdentity windows) sid = windows.User?.Value;
        if (string.IsNullOrWhiteSpace(sid)) return IdentityErrors.WindowsSidRequired;
        if (principal.Identity.Name is not { } account) return IdentityErrors.WindowsAccountRequired;
        var displayName = principal.FindFirstValue("display_name") ?? names.GetOrAdd(sid, () => ResolveName(sid, account));
        // Most requests only read an existing mapping. The global gate is taken only to create, bind or refresh a user,
        // or to apply a bootstrap grant: binding goes by account across SIDs, and local sign-in writes the same rows.
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, ct);
        if (existing is not null && !SessionIdentity.Allows(existing, "ad")) return IdentityErrors.LoginMethodDisabled;
        if (existing is not null && !Stale(existing, displayName) && !await PendingGrantAsync(existing, ct))
            return resolved = Track(existing)!;
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Sid == sid, ct);
            if (user is null)
            {
                var name = UserAccounts.AccountName(account);
                if (!UserAccounts.IsValid(name)) return IdentityErrors.InvalidAccount;
                var adAccount = UserAccounts.Normalize(name);
                user = await db.Users.SingleOrDefaultAsync(x => x.AdAccount == adAccount, ct);
                if (user is not null && user.Sid.StartsWith("managed:", StringComparison.Ordinal))
                { user.Sid = sid; user.Account = account; }
                else if (user is not null) return IdentityErrors.AdBindingConflict;
            }
            if (user is not null && !SessionIdentity.Allows(user, "ad")) return IdentityErrors.LoginMethodDisabled;
            if (user is null)
            {
                user = new NexusUser { Sid = sid, Account = account, DisplayName = displayName, LastSeenAt = clock.GetUtcNow() };
                db.Users.Add(user);
                db.Set<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = BuiltInAccess.MemberRole });
                db.AuditEvents.Add(new AuditEvent { OwnerId = user.Id, Action = "identity.first_seen", ResourceId = user.Id });
                await db.SaveChangesAsync(ct);
            }
            else if (Stale(user, displayName) || db.Entry(user).State == EntityState.Modified)
            {
                user.Account = account;
                if (!user.ProfileManaged) user.DisplayName = displayName;
                user.LastSeenAt = clock.GetUtcNow();
                await db.SaveChangesAsync(ct);
            }
            foreach (var grant in grants) await grant.ApplyAsync(user, ct);
            return resolved = user;
        }
        finally { writeLock.Gate.Release(); }
    }

    private async Task<bool> PendingGrantAsync(NexusUser user, CancellationToken ct)
    {
        foreach (var grant in grants)
            if (await grant.PendingAsync(user, ct)) return true;
        return false;
    }

    private bool Stale(NexusUser user, string displayName) => clock.GetUtcNow() - user.LastSeenAt > SeenInterval || !user.ProfileManaged && user.DisplayName != displayName;

    /// <summary>The request's tracked copy of a user read without tracking; handlers may change and save it.</summary>
    private NexusUser? Track(NexusUser? user)
    {
        if (user is null) return null;
        var tracked = db.ChangeTracker.Entries<NexusUser>().FirstOrDefault(x => x.Entity.Id == user.Id);
        if (tracked is not null) return tracked.Entity;
        db.Attach(user);
        return user;
    }

    private static string ResolveName(string sid, string account)
    {
        var name = account.Split('\\').Last();
        if (OperatingSystem.IsWindows()) name = ResolveWindowsName(sid) ?? name;
        return name;
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveWindowsName(string sid)
    {
        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.Sid, sid);
            return user?.DisplayName;
        }
        catch (PrincipalException) { return null; }
        catch (SystemException) { return null; }
    }
}
