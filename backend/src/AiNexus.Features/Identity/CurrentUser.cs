using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.Roles;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity;

/// <summary>目前請求的使用者解析器。首次解析時把 Windows SID 對應到 <see cref="NexusUser"/>（必要時建立、綁定或更新），結果在同一請求內快取。</summary>
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
            // Cookie 驗證剛讀過這一列，直接追蹤那份副本，避免再查一次。
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
        // 多數請求只讀既有的對應，不取全域寫入鎖；只有建立、綁定或更新使用者，或套用初始授權時才取鎖。
        // 綁定是依帳號跨 SID 進行，且本地登入寫的也是同一批資料列，所以鎖必須共用。
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

    /// <summary>回傳本請求中追蹤的使用者實體（未追蹤則 Attach 進來），讓 handler 可以修改並儲存。</summary>
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
