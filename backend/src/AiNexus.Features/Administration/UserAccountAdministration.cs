using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed record UserAccountRequest(string DisplayName, bool Enabled, bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, IReadOnlyList<string> RoleIds, string? Password = null);
public sealed record CreatedUserDto(Guid Id);

/// <summary>
/// Login account names are checked first, with their own code; the name, roles and login methods are checked
/// afterwards by <see cref="UserAccountAdministration"/>, in that order.
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

public sealed class UserAccountAdministration(NexusDbContext db, CurrentUser current, AdministrativeAudit audit, Argon2Passwords passwords, IHttpContextAccessor http)
{
    public async Task<Guid> SaveAsync(Guid? id, UserAccountRequest request, CancellationToken ct)
    {
        var actor = await current.GetAsync(ct);
        var local = UserAccounts.Normalize(request.LocalAccount); var ad = UserAccounts.Normalize(request.AdAccount);
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120 || request.RoleIds is null || request.RoleIds.Count > 100 || request.RoleIds.Distinct().Count() != request.RoleIds.Count)
            throw new ApiException(400, "invalid_user", "請輸入姓名與有效角色。");
        if (!request.AdEnabled && !request.LocalEnabled || request.AdEnabled && ad is null || request.LocalEnabled && local is null)
            throw new ApiException(400, "invalid_login_methods", "請至少設定一種登入方式，並填寫對應的登入帳號。");
        var hash = request.Password is { Length: > 0 } password ? await passwords.HashAsync(password, ct) : null;
        var key = id ?? Guid.NewGuid();
        await audit.MutateAsync("admin.user", key, key.ToString(), async () =>
        {
            var user = id is null ? new NexusUser { Id = key, Sid = "managed:" + key, ProfileManaged = true, LastSeenAt = DateTimeOffset.UtcNow } :
                await db.Users.SingleOrDefaultAsync(x => x.Id == key && x.DeletedAt == null, ct) ?? throw new ApiException(404, "user_not_found", "找不到使用者。");
            if (request.LocalEnabled && hash is null && user.PasswordHash is null)
                throw new ApiException(400, "local_password_required", "首次啟用本地登入時，請設定密碼。");
            var currentMethod = http.HttpContext?.User.FindFirst(SessionIdentity.Method)?.Value ?? "ad";
            if (key == actor.Id && (!request.Enabled || currentMethod == "local" && !request.LocalEnabled || currentMethod != "local" && !request.AdEnabled))
                throw new ApiException(409, "admin_lockout", "不能停用自己或目前使用的登入方式。請由另一位管理員處理。");
            if (await db.Users.AnyAsync(x => x.Id != key && (local != null && x.LocalAccount == local || ad != null && x.AdAccount == ad), ct))
                throw new ApiException(409, "account_exists", "此登入帳號已由其他使用者使用或保留。");
            // A pre-provisioned AD name must not take over an existing, SID-bound user.
            if (ad is not null)
            {
                var accounts = await db.Users.AsNoTracking().Where(x => x.Id != key && x.AdAccount == null && !x.Sid.StartsWith("managed:")).Select(x => x.Account).ToListAsync(ct);
                if (accounts.Any(x => string.Equals(UserAccounts.AccountName(x), ad, StringComparison.OrdinalIgnoreCase)))
                    throw new ApiException(409, "account_exists", "此 AD 帳號已有使用者，請直接編輯既有使用者。");
                if (!user.Sid.StartsWith("managed:") && !string.Equals(UserAccounts.AccountName(user.Account), ad, StringComparison.OrdinalIgnoreCase))
                    throw new ApiException(409, "ad_binding_immutable", "已連結的 AD 身分不能換綁其他帳號；請建立另一位使用者。");
            }
            var validRoles = await db.Set<Role>().CountAsync(x => request.RoleIds.Contains(x.Id), ct);
            if (validRoles != request.RoleIds.Count) throw new ApiException(400, "invalid_roles", "選取的角色已不存在，請重新載入。");
            if (id is null) db.Users.Add(user);
            if (user.Enabled != request.Enabled || user.AdEnabled != request.AdEnabled || user.LocalEnabled != request.LocalEnabled || user.AdAccount != ad || user.LocalAccount != local || hash is not null)
                user.SecurityVersion++;
            user.DisplayName = request.DisplayName.Trim(); user.ProfileManaged = true;
            user.Enabled = request.Enabled; user.AdEnabled = request.AdEnabled; user.LocalEnabled = request.LocalEnabled;
            user.AdAccount = ad; user.LocalAccount = local;
            if (user.Sid.StartsWith("managed:")) user.Account = local ?? ad!;
            if (hash is not null) { user.PasswordHash = hash; user.FailedLogins = 0; user.LockedUntil = null; }
            var roles = await db.Set<UserRole>().Where(x => x.UserId == key).ToListAsync(ct);
            db.RemoveRange(roles.Where(x => !request.RoleIds.Contains(x.RoleId)));
            db.AddRange(request.RoleIds.Where(x => roles.All(y => y.RoleId != x)).Select(x => new UserRole { UserId = key, RoleId = x }));
        }, ct);
        return key;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await audit.MutateAsync("admin.user_delete", id, id.ToString(), async () =>
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct) ?? throw new ApiException(404, "user_not_found", "找不到使用者。");
            user.DeletedAt = DateTimeOffset.UtcNow; user.Enabled = false; user.AdEnabled = false; user.LocalEnabled = false;
            user.PasswordHash = null; user.SecurityVersion++;
            // Keep SID, names, resources and historical relationships to prevent takeover or cascading loss.
        }, ct);
    }
}
