using System.Collections.Concurrent;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using AiNexus.Features.Persistence;
using AiNexus.Features.Operations;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity;

public sealed class IdentityWriteLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

/// <summary>Directory display names by SID for an hour, bounded so a host's lifetime cannot grow it without limit.</summary>
public sealed class DisplayNameCache(TimeProvider clock)
{
    private const int Capacity = 4096;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<string, (string Name, DateTimeOffset At)> names = new(StringComparer.Ordinal);

    public string GetOrAdd(string sid, Func<string> resolve)
    {
        var now = clock.GetUtcNow();
        if (names.TryGetValue(sid, out var cached) && now - cached.At < Lifetime) return cached.Name;
        var name = resolve();
        if (names.Count >= Capacity)
        {
            foreach (var entry in names) if (now - entry.Value.At >= Lifetime) names.TryRemove(entry);
            if (names.Count >= Capacity) names.Clear();
        }
        names[sid] = (name, now);
        return name;
    }
}

public sealed class CurrentUser(NexusDbContext db, IHttpContextAccessor accessor, StorageReadiness storage, IdentityWriteLock writeLock, IEnumerable<ISignInGrant> grants,
    DisplayNameCache names, TimeProvider clock) : IRequestUser, ICurrentUser
{
    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(5);
    private NexusUser? resolved;
    public Guid? ResolvedId => resolved?.Id;
    Guid ICurrentUser.Id => ((ICurrentUser)this).User.Id;
    NexusUser ICurrentUser.User => resolved ?? throw new InvalidOperationException("ICurrentUser is resolved by the /api/v1 endpoint filter; declare it as a handler parameter.");

    public async Task<NexusUser> GetAsync(CancellationToken ct = default)
    {
        if (resolved is not null) return resolved;
        storage.RequireConfigured();
        var principal = accessor.HttpContext?.User ?? throw new ApiException(401, "authentication_required", "請使用公司 Windows 帳號登入。");
        if (principal.Identity?.IsAuthenticated != true) throw new ApiException(401, "authentication_required", "AD 驗證未完成。");
        if (Guid.TryParse(principal.FindFirstValue(SessionIdentity.UserId), out var userId))
        {
            // Cookie validation has just read this row; track that copy instead of reading it again.
            var linked = Track(SessionIdentity.ValidatedUser(accessor.HttpContext!, userId)) ?? await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
            var testing = principal.HasClaim(x => x.Type == SessionIdentity.ActorId);
            if (linked is null || !SessionIdentity.Available(linked) || !SessionIdentity.MatchesVersion(linked, principal.FindFirstValue(SessionIdentity.Version)) ||
                !testing && !SessionIdentity.Allows(linked, principal.FindFirstValue(SessionIdentity.Method)))
                throw new ApiException(401, "session_revoked", "登入已失效，請重新登入工作區。");
            return resolved = linked;
        }
        var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
        if (sid is null && OperatingSystem.IsWindows() && principal.Identity is WindowsIdentity windows) sid = windows.User?.Value;
        if (string.IsNullOrWhiteSpace(sid)) throw new ApiException(403, "windows_sid_required", "無法取得 AD SID，請確認登入設定。");
        var account = principal.Identity.Name ?? throw new ApiException(403, "windows_account_required", "Windows 帳號資料不完整。");
        var displayName = principal.FindFirstValue("display_name") ?? names.GetOrAdd(sid, () => ResolveName(sid, account));
        // Most requests only read an existing mapping. The global gate is taken only to create, bind or refresh a user,
        // or to apply a bootstrap grant: binding goes by account across SIDs, and local sign-in writes the same rows.
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, ct);
        if (existing is not null && !SessionIdentity.Allows(existing, "ad"))
            throw new ApiException(403, "login_method_disabled", "此使用者已停用，或未允許 AD 驗證。");
        if (existing is not null && !Stale(existing, displayName) && !await PendingGrantAsync(existing, ct))
            return resolved = Track(existing)!;
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Sid == sid, ct);
            if (user is null)
            {
                var adAccount = UserAccounts.Normalize(UserAccounts.AccountName(account));
                user = await db.Users.SingleOrDefaultAsync(x => x.AdAccount == adAccount, ct);
                if (user is not null && user.Sid.StartsWith("managed:"))
                { user.Sid = sid; user.Account = account; }
                else if (user is not null)
                    throw new ApiException(403, "ad_binding_conflict", "AD 身分與已連結帳號不一致，請聯絡管理員。");
            }
            if (user is not null && !SessionIdentity.Allows(user, "ad"))
                throw new ApiException(403, "login_method_disabled", "此使用者已停用，或未允許 AD 驗證。");
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
