using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Identity;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity.Management;

public sealed record UserAccountRequest(string DisplayName, bool Enabled, bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, IReadOnlyList<string> RoleIds, string? Password = null);

public sealed record CreatedUserDto(Guid Id);

/// <summary>
/// 登入帳號格式先在此檢查並回傳專屬代碼；名稱衝突、角色與登入方式則之後在 <see cref="ManageUserAccounts"/> 的稽核交易內檢查，順序不可對調。
/// </summary>
internal sealed class UserAccountRequestValidator : RequestValidator<UserAccountRequest>
{
    public override string ProblemCode => "invalid_account";

    public UserAccountRequestValidator()
    {
        RuleFor(x => x.LocalAccount).Must(UserAccounts.IsValid).WithErrorCode("format");
        RuleFor(x => x.AdAccount).Must(UserAccounts.IsValid).WithErrorCode("format");
    }
}

/// <summary>建立、編輯、刪除帳號；每次變更都經由 <see cref="AdministrativeAudit"/> 稽核。</summary>
internal sealed class ManageUserAccounts(AppDbContext db, AdministrativeAudit audit, Argon2Passwords passwords, IHttpContextAccessor http, TimeProvider clock)
{
    public static void MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/users", (UserAccountRequest body, ICurrentUser user, ManageUserAccounts handler, CancellationToken ct) => handler.SaveAsync(user.Id, null, body, ct).ToHttpResultAsync(id => TypedResults.Ok(new CreatedUserDto(id))))
        .WithName("CreateAdminUser");

    public static void MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}", (Guid id, UserAccountRequest body, ICurrentUser user, ManageUserAccounts handler, CancellationToken ct) => handler.SaveAsync(user.Id, id, body, ct).ToHttpResultAsync(_ => TypedResults.NoContent()))
        .WithName("SaveAdminUser");

    public static void MapDelete(RouteGroupBuilder routes) => routes
        .MapDelete("/users/{id:guid}", (Guid id, ICurrentUser user, ManageUserAccounts handler, CancellationToken ct) => handler.DeleteAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DeleteAdminUser");

    public async Task<Result<Guid>> SaveAsync(Guid actor, Guid? id, UserAccountRequest request, CancellationToken ct)
    {
        var local = UserAccounts.Normalize(request.LocalAccount); var ad = UserAccounts.Normalize(request.AdAccount);
        var displayNameInvalid = string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120;
        // 角色清單要在這裡就確認非 null，編譯器才能保證後面的 RoleIds 使用安全；不要抽成布林變數。
        if (displayNameInvalid || request.RoleIds is null || request.RoleIds.Count > 100
            || request.RoleIds.Distinct().Count() != request.RoleIds.Count)
            return IdentityErrors.InvalidUser;
        var noLoginMethod = !request.AdEnabled && !request.LocalEnabled;
        var missingAccount = (request.AdEnabled && ad is null) || (request.LocalEnabled && local is null);
        if (noLoginMethod || missingAccount) return IdentityErrors.InvalidLoginMethods;
        if (request.Password is { Length: > 0 } candidate && !Argon2Passwords.IsAcceptable(candidate))
            return IdentityErrors.InvalidPassword;
        var hash = request.Password is { Length: > 0 } password ? await passwords.HashAsync(password, ct) : null;
        var key = id ?? Guid.NewGuid();
        var saved = await audit.MutateAsync(actor, "admin.user", key, key.ToString(), token => SnapshotAsync(key, token), async () =>
        {
            var user = id is null ? new User { Id = key, Sid = "managed:" + key, ProfileManaged = true, LastSeenAt = clock.GetUtcNow() } :
                await db.Users.SingleOrDefaultAsync(x => x.Id == key && x.DeletedAt == null, ct);
            if (user is null) return IdentityErrors.UserNotFound;
            if (request.LocalEnabled && hash is null && user.PasswordHash is null)
                return IdentityErrors.LocalPasswordRequired;
            var currentMethod = http.HttpContext?.User.FindFirst(SessionIdentity.Method)?.Value ?? "ad";
            var disablesSelf = !request.Enabled
                || (currentMethod == "local" && !request.LocalEnabled)
                || (currentMethod != "local" && !request.AdEnabled);
            if (key == actor && disablesSelf) return AccessControlErrors.Lockout;
            if (await db.Users.AnyAsync(x => x.Id != key && ((local != null && x.LocalAccount == local) || (ad != null && x.AdAccount == ad)), ct))
                return IdentityErrors.AccountExists;
            // 綁定 AD 帳號時，不可與既有 AD 使用者同名（等於接管他的身分），也不可改動由 AD 同步建立的帳號名稱。
            if (ad is not null)
            {
                var accounts = await db.Users.AsNoTracking().Where(x => x.Id != key && x.AdAccount == null && !x.Sid.StartsWith("managed:")).Select(x => x.Account).ToListAsync(ct);
                if (accounts.Any(x => string.Equals(UserAccounts.AccountName(x), ad, StringComparison.OrdinalIgnoreCase)))
                    return IdentityErrors.AccountExists;
                if (!user.Sid.StartsWith("managed:", StringComparison.Ordinal) && !string.Equals(UserAccounts.AccountName(user.Account), ad, StringComparison.OrdinalIgnoreCase))
                    return IdentityErrors.AdBindingImmutable;
            }
            var validRoles = await db.Set<Role>().CountAsync(x => request.RoleIds.Contains(x.Id), ct);
            if (validRoles != request.RoleIds.Count) return IdentityErrors.InvalidRoles;
            if (id is null) db.Users.Add(user);
            var flagsChanged = user.Enabled != request.Enabled || user.AdEnabled != request.AdEnabled || user.LocalEnabled != request.LocalEnabled;
            var credentialsChanged = user.AdAccount != ad || user.LocalAccount != local || hash is not null;
            if (flagsChanged || credentialsChanged) user.SecurityVersion++;
            user.DisplayName = request.DisplayName.Trim(); user.ProfileManaged = true;
            user.Enabled = request.Enabled; user.AdEnabled = request.AdEnabled; user.LocalEnabled = request.LocalEnabled;
            user.AdAccount = ad; user.LocalAccount = local;
            if (user.Sid.StartsWith("managed:", StringComparison.Ordinal)) user.Account = local ?? ad!;
            if (hash is not null) { user.PasswordHash = hash; user.FailedLogins = 0; user.LockedUntil = null; }
            var roles = await db.Set<UserRole>().Where(x => x.UserId == key).ToListAsync(ct);
            db.RemoveRange(roles.Where(x => !request.RoleIds.Contains(x.RoleId)));
            db.AddRange(request.RoleIds.Where(x => roles.All(y => y.RoleId != x)).Select(x => new UserRole { UserId = key, RoleId = x }));
            return Result.Success;
        }, ct);
        return saved.IsSuccess ? key : saved.Error;
    }

    public Task<Result> DeleteAsync(Guid actor, Guid id, CancellationToken ct) => audit.MutateAsync(actor, "admin.user_delete", id, id.ToString(), token => SnapshotAsync(id, token), async () =>
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (user is null) return IdentityErrors.UserNotFound;
            user.DeletedAt = clock.GetUtcNow(); user.Enabled = false; user.AdEnabled = false; user.LocalEnabled = false;
            user.PasswordHash = null; user.SecurityVersion++;
            // 只做軟刪除並保留 SID、帳號名與關聯資料：帳號名不會被重新註冊接管，歷史資料也不會連帶遺失。
            return Result.Success;
        }, ct);

    /// <summary>建立、編輯與刪除帳號共用的稽核快照（<c>admin.user</c>、<c>admin.user_delete</c>）。</summary>
    private async Task<object?> SnapshotAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return null;
        var roleIds = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == id).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(ct);
        return new
        {
            user.Account,
            user.DisplayName,
            user.Enabled,
            user.DeletedAt,
            user.SecurityVersion,
            authentication = UserAccounts.Authentication(user),
            roleIds
        };
    }
}
