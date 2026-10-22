using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.SelfService;

/// <summary>本地登入變更了什麼；呼叫端依此寫稽核與通知。</summary>
public sealed record LocalLoginChange(bool Enabled, bool Renamed, bool PasswordChanged);

/// <summary>
/// 使用者自行變更的信箱與本地登入。這裡只檢查並修改追蹤中的 <see cref="User"/>（含 <c>SecurityVersion++</c>），不呼叫 <c>SaveChanges</c>：
/// 呼叫端與稽核、通知信一起儲存。<c>SecurityVersion</c> 是並行 token，期間有其他變更時儲存會失敗，不會覆蓋。
/// </summary>
public sealed class UserCredentials(AppDbContext db, Argon2Passwords passwords, IOptions<SelfServiceOptions> options, TimeProvider clock)
{
    /// <summary>本人能否設定信箱：AD 提供的信箱由目錄維護。</summary>
    public static bool EmailEditable(User user) => user.EmailSource != EmailSources.Directory;

    /// <summary>本人能否自行啟用或變更本地登入（設定開放時）。</summary>
    public bool LocalLoginEditable => options.Value.LocalLogin;

    /// <summary>
    /// 檢查要設定的新信箱並回傳正規化值：格式、目錄維護的信箱不可改，也不可是他人已驗證的地址（避免日後以信箱找回帳號時被接管）。
    /// </summary>
    public async Task<Result<string>> CheckEmailAsync(User user, string? email, CancellationToken ct)
    {
        if (!EmailEditable(user)) return IdentityErrors.EmailManagedByDirectory;
        if (!UserEmails.TryNormalize(email, out var address)) return IdentityErrors.EmailInvalid;
        // 資料庫定序不分大小寫，等號比對即涵蓋大小寫不同的同一地址。
        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.DeletedAt == null && x.Email == address && x.EmailVerifiedAt != null, ct)) return IdentityErrors.EmailInUse;
        return address;
    }

    /// <summary>驗證碼確認後寫入本人驗證的信箱。</summary>
    public void SetVerifiedEmail(User user, string address)
    {
        user.Email = address;
        user.EmailSource = EmailSources.User;
        user.EmailVerifiedAt = clock.GetUtcNow();
        user.SecurityVersion++;
    }

    /// <summary>
    /// 啟用或變更本地登入。帳號名空白時沿用目前的本地帳號；新名稱不可與他人的本地帳號或 AD 帳號相同（防冒名），可以等於自己的 AD 帳號。
    /// 密碼要通過長度規則與 <see cref="PasswordPolicy"/>。完成後重設失敗次數與鎖定。
    /// </summary>
    public async Task<Result<LocalLoginChange>> SetLocalLoginAsync(User user, string? account, string? password, CancellationToken ct)
    {
        if (!options.Value.LocalLogin) return IdentityErrors.LocalLoginUnavailable;
        var requested = string.IsNullOrWhiteSpace(account) ? user.LocalAccount : account;
        if (string.IsNullOrWhiteSpace(requested) || !UserAccounts.IsValid(requested)) return IdentityErrors.InvalidAccount;
        var name = UserAccounts.Normalize(requested)!;
        if (password is null || !Argon2Passwords.IsAcceptable(password)) return IdentityErrors.InvalidPassword;
        if (!PasswordPolicy.IsStrong(password, name, user.AdAccount, UserAccounts.AccountName(user.Account))) return IdentityErrors.PasswordWeak;
        var renamed = name != user.LocalAccount;
        if (renamed && await TakenAsync(user, name, ct)) return IdentityErrors.AccountExists;
        var enabled = !user.LocalEnabled || user.PasswordHash is null;
        user.LocalAccount = name;
        user.PasswordHash = await passwords.HashAsync(password, ct);
        user.PasswordChangedAt = clock.GetUtcNow();
        user.LocalEnabled = true; user.FailedLogins = 0; user.LockedUntil = null;
        // 尚未綁定 AD 的預建帳號以本地帳號顯示，與管理端建立帳號的規則相同。
        if (user.Sid.StartsWith("managed:", StringComparison.Ordinal)) user.Account = name;
        user.SecurityVersion++;
        return new LocalLoginChange(enabled, renamed && !enabled, !enabled);
    }

    /// <summary>停用本地登入並清除密碼；保留本地帳號名，避免名稱立刻被他人取用。本地是唯一登入方式時拒絕。</summary>
    public static Result<bool> DisableLocalLogin(User user)
    {
        if (!user.AdEnabled) return IdentityErrors.LocalLoginRequired;
        if (!user.LocalEnabled && user.PasswordHash is null) return false;
        user.LocalEnabled = false; user.PasswordHash = null; user.FailedLogins = 0; user.LockedUntil = null;
        user.SecurityVersion++;
        return true;
    }

    private async Task<bool> TakenAsync(User user, string name, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(x => x.Id != user.Id && (x.LocalAccount == name || x.AdAccount == name), ct)) return true;
        // 較早由 AD 首次登入自動建立的使用者沒有 AdAccount，要比對完整帳號的短名（與管理端建立帳號同一規則）。
        var accounts = await db.Users.AsNoTracking().Where(x => x.Id != user.Id && x.AdAccount == null && !x.Sid.StartsWith("managed:")).Select(x => x.Account).ToListAsync(ct);
        return accounts.Any(x => string.Equals(UserAccounts.AccountName(x), name, StringComparison.OrdinalIgnoreCase));
    }
}
