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

public sealed class CurrentUser(NexusDbContext db, IHttpContextAccessor accessor, StorageReadiness storage, IdentityWriteLock writeLock, AiNexus.Features.Administration.AdminBootstrap bootstrap) : IRequestUser, ICurrentUser
{
    private static readonly ConcurrentDictionary<string, (string Name, DateTimeOffset At)> DisplayNames = new();
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
            var linked = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
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
        var displayName = principal.FindFirstValue("display_name") ?? ResolveName(sid, account);
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Sid == sid, ct);
            if (user is null)
            {
                var adAccount = UserAccounts.Normalize(AiNexus.Features.Administration.AdminBootstrap.AccountName(account));
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
                user = new NexusUser { Sid = sid, Account = account, DisplayName = displayName, LastSeenAt = DateTimeOffset.UtcNow };
                db.Users.Add(user);
                db.Set<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = BuiltInAccess.MemberRole });
                db.AuditEvents.Add(new AuditEvent { OwnerId = user.Id, Action = "identity.first_seen", ResourceId = user.Id });
                await db.SaveChangesAsync(ct);
            }
            else if (DateTimeOffset.UtcNow - user.LastSeenAt > TimeSpan.FromMinutes(5) || !user.ProfileManaged && user.DisplayName != displayName || db.Entry(user).State == EntityState.Modified)
            {
                user.Account = account;
                if (!user.ProfileManaged) user.DisplayName = displayName;
                user.LastSeenAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            await bootstrap.ApplyAsync(user, ct);
            return resolved = user;
        }
        finally { writeLock.Gate.Release(); }
    }

    private static string ResolveName(string sid, string account)
    {
        if (DisplayNames.TryGetValue(sid, out var cached) && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromHours(1)) return cached.Name;
        var name = account.Split('\\').Last();
        if (OperatingSystem.IsWindows()) name = ResolveWindowsName(sid) ?? name;
        DisplayNames[sid] = (name, DateTimeOffset.UtcNow);
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
