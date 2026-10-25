using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Identity;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity;

/// <summary>
/// 目前請求的使用者解析器。首次解析時把 Windows SID 對應到 <see cref="User"/>（必要時建立、綁定或更新），結果在同一請求內快取。
/// 建立與同步的規則在 <see cref="DirectoryAccounts"/>，與管理員從 AD 匯入、更新共用。
/// </summary>
public sealed class CurrentUser(AppDbContext db, IHttpContextAccessor accessor, StorageReadiness storage, IdentityWriteLock writeLock, IEnumerable<ISignInGrant> grants,
    DirectoryProfileCache profiles, DirectoryAccounts accounts, TimeProvider clock) : IRequestUser, ICurrentUser, IRequestActor
{
    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(5);
    private User? resolved;
    public Guid? ResolvedId => resolved?.Id;
    Guid ICurrentUser.Id => ((ICurrentUser)this).User.Id;
    Guid IRequestActor.Id => ((ICurrentUser)this).User.Id;
    User ICurrentUser.User => resolved ?? throw new InvalidOperationException("ICurrentUser is resolved by the /api/v1 endpoint filter; declare it as a handler parameter.");

    public async Task<Result<User>> GetAsync(CancellationToken ct = default)
    {
        if (resolved is not null) return resolved;
        storage.RequireConfigured();
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return IdentityErrors.AuthenticationRequired;
        if (Guid.TryParse(principal.FindFirstValue(SessionIdentity.UserId), out var userId))
        {
            // Cookie 驗證剛讀過這一列，直接追蹤那份副本，避免再查一次。
            var linked = Track(SessionIdentity.ValidatedUser(accessor.HttpContext!, userId)) ?? await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
            var testing = SessionIdentity.IsTest(principal);
            if (linked is null || !ActiveUsers.IsActive(linked)
                || !SessionIdentity.MatchesVersion(linked, principal.FindFirstValue(SessionIdentity.Version))
                || (!testing && !SessionIdentity.Allows(linked, principal.FindFirstValue(SessionIdentity.Method))))
                return IdentityErrors.SessionRevoked;
            return resolved = linked;
        }
        var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
        if (sid is null && OperatingSystem.IsWindows() && principal.Identity is WindowsIdentity windows) sid = windows.User?.Value;
        if (string.IsNullOrWhiteSpace(sid)) return IdentityErrors.WindowsSidRequired;
        if (principal.Identity.Name is not { } account) return IdentityErrors.WindowsAccountRequired;
        // LDAP 登入由 LogIn 帶入目錄的姓名與信箱；Windows 整合驗證則依 SID 查 AD（快取一小時）。
        var profile = principal.FindFirstValue(SessionIdentity.DisplayName) is { } claimed
            ? new DirectoryProfile(claimed, principal.FindFirstValue(SessionIdentity.DirectoryEmail))
            : profiles.GetOrAdd(sid, () => ResolveProfile(sid, account));
        // 多數請求只讀既有的對應，不取全域寫入鎖；只有建立、綁定或更新使用者，或套用初始授權時才取鎖。
        // 綁定是依帳號跨 SID 進行，且本地登入與管理員匯入寫的也是同一批資料列，所以鎖必須共用。
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, ct);
        if (existing is not null && !SessionIdentity.Allows(existing, "ad")) return IdentityErrors.LoginMethodDisabled;
        if (existing is not null && !Stale(existing, account, profile) && !await PendingGrantAsync(existing, ct))
            return resolved = Track(existing)!;
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            var match = await accounts.MatchAsync(sid, account, ct);
            if (!match.IsSuccess) return match.Error;
            var user = match.Value.Existing;
            if (user is not null && !SessionIdentity.Allows(user, "ad")) return IdentityErrors.LoginMethodDisabled;
            var now = clock.GetUtcNow();
            if (user is null)
            {
                user = await accounts.CreateAsync(Guid.NewGuid(), sid, account, profile, now, ct);
                // 走到這裡的請求都剛通過 AD（LDAP bind 或 Kerberos／NTLM），是本地密碼新鮮度規則的依據；管理員匯入的帳號則留空，直到本人登入。
                user.DirectoryVerifiedAt = now;
                db.AuditEvents.Add(new AuditEvent { OwnerId = user.Id, Action = "identity.first_seen", ResourceId = user.Id });
                await db.SaveChangesAsync(ct);
            }
            else if (Stale(user, account, profile) || db.Entry(user).State == EntityState.Modified)
            {
                DirectoryAccounts.Apply(user, await accounts.ChangesAsync(user, account, profile, ct), now);
                user.LastSeenAt = now; user.DirectoryVerifiedAt = now;
                await db.SaveChangesAsync(ct);
            }
            foreach (var grant in grants) await grant.ApplyAsync(user, ct);
            return resolved = user;
        }
        finally { writeLock.Gate.Release(); }
    }

    private async Task<bool> PendingGrantAsync(User user, CancellationToken ct)
    {
        foreach (var grant in grants)
            if (await grant.PendingAsync(user, ct)) return true;
        return false;
    }

    private bool Stale(User user, string account, DirectoryProfile profile)
        => clock.GetUtcNow() - user.LastSeenAt > SeenInterval || user.DirectoryVerifiedAt is null || DirectoryAccounts.Differs(user, account, profile);

    /// <summary>回傳本請求中追蹤的使用者實體（未追蹤則 Attach 進來），讓 handler 可以修改並儲存。</summary>
    private User? Track(User? user)
    {
        if (user is null) return null;
        var tracked = db.ChangeTracker.Entries<User>().FirstOrDefault(x => x.Entity.Id == user.Id);
        if (tracked is not null) return tracked.Entity;
        db.Attach(user);
        return user;
    }

    private static DirectoryProfile ResolveProfile(string sid, string account)
    {
        var name = account.Split('\\').Last();
        var resolved = OperatingSystem.IsWindows() ? ResolveWindowsProfile(sid) : null;
        return new(resolved?.DisplayName is { Length: > 0 } display ? display : name, resolved?.Email);
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryProfile? ResolveWindowsProfile(string sid)
    {
        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.Sid, sid);
            return user is null ? null : new(user.DisplayName ?? "", user.EmailAddress);
        }
        catch (PrincipalException) { return null; }
        catch (SystemException) { return null; }
    }
}
