using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
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
/// 目前請求的使用者解析器，結果在同一請求內快取。有 session cookie 時依 cookie 的使用者，
/// 否則（Windows 整合驗證、LDAP 登入當下）交給 <see cref="DirectorySignIn"/> 由 SID 對應。
/// </summary>
public sealed class CurrentUser(AppDbContext db, IHttpContextAccessor accessor, StorageReadiness storage, DirectorySignIn directory) : IRequestUser, ICurrentUser, IRequestActor
{
    private User? resolved;
    public Guid? ResolvedId => resolved?.Id;
    private User Resolved => resolved ?? throw new InvalidOperationException("ICurrentUser is resolved by the /api/v1 endpoint filter; declare it as a handler parameter.");
    Guid ICurrentUser.Id => Resolved.Id;
    User ICurrentUser.User => Resolved;
    Guid IRequestActor.Id => Resolved.Id;

    public async Task<Result<User>> GetAsync(CancellationToken ct = default)
    {
        if (resolved is not null) return resolved;
        storage.RequireConfigured();
        var http = accessor.HttpContext;
        var principal = http?.User;
        if (principal?.Identity?.IsAuthenticated != true) return IdentityErrors.AuthenticationRequired;
        var user = Guid.TryParse(principal.FindFirstValue(SessionIdentity.UserId), out var userId)
            ? await SessionUserAsync(http!, principal, userId, ct)
            : await DirectoryUserAsync(principal, ct);
        if (!user.IsSuccess) return user;
        return resolved = Track(user.Value);
    }

    private async Task<Result<User>> SessionUserAsync(HttpContext http, ClaimsPrincipal principal, Guid userId, CancellationToken ct)
    {
        // Cookie 驗證剛讀過這一列就沿用；其他情況（切回原管理者、長串流在新 scope 重驗）重查，撤銷才會生效。
        var user = SessionIdentity.TakeValidatedUser(http, userId) ?? await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        return user is not null && SessionIdentity.Accepts(user, principal) ? user : IdentityErrors.SessionRevoked;
    }

    private async Task<Result<User>> DirectoryUserAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var sid = principal.FindFirstValue(ClaimTypes.PrimarySid);
        if (sid is null && OperatingSystem.IsWindows() && principal.Identity is WindowsIdentity windows) sid = windows.User?.Value;
        if (string.IsNullOrWhiteSpace(sid)) return IdentityErrors.WindowsSidRequired;
        if (principal.Identity?.Name is not { } account) return IdentityErrors.WindowsAccountRequired;
        // LDAP 登入由 LogIn 帶入目錄的姓名與信箱；Windows 整合驗證不帶，由 DirectorySignIn 需要同步時才查 AD。
        var known = principal.FindFirstValue(SessionIdentity.DisplayName) is { } name
            ? new DirectoryProfile(name, principal.FindFirstValue(SessionIdentity.DirectoryEmail))
            : null;
        return await directory.ResolveAsync(sid, account, known, ct);
    }

    /// <summary>回傳本請求中追蹤的使用者實體（未追蹤則 Attach 進來），讓 handler 可以修改並儲存。</summary>
    private User Track(User user)
    {
        var tracked = db.ChangeTracker.Entries<User>().FirstOrDefault(x => x.Entity.Id == user.Id);
        if (tracked is not null) return tracked.Entity;
        db.Attach(user);
        return user;
    }
}
