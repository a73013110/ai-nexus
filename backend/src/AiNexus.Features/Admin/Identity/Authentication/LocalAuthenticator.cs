using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// 本地帳密驗證。雜湊比對耗時，所以比對時不持鎖；比對完才鎖住這位使用者的資料列並重讀，讓撤銷與並行登入都以較新的狀態為準，失敗次數也不會互相蓋掉。
/// 規則只涉及這一列，所以不同帳號的登入互不等待；列鎖跨程序有效。
/// 有設定 <see cref="SelfServiceOptions.DirectoryFreshnessDays"/>（大於 0）時，AD 綁定且仍允許 AD 登入的使用者，最近一次 AD 驗證超過該天數就拒絕本地密碼：
/// AD 帳號停用（離職）後，自行設定的本地密碼最晚在這個期限後失效。管理員把 AD 登入關掉的帳號是明確的本地專用帳號，不套用。
/// </summary>
public sealed class LocalAuthenticator(AppDbContext db, Argon2Passwords passwords, IOptions<SelfServiceOptions> selfService, TimeProvider clock)
{
    public async Task<Result<User>> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        var normalized = UserAccounts.IsValid(account) ? UserAccounts.Normalize(account) : null;
        var user = normalized is null ? null : await db.Users.SingleOrDefaultAsync(x => x.LocalAccount == normalized, ct);
        var checkedHash = user?.PasswordHash;
        var checkedVersion = user?.SecurityVersion;
        var result = await passwords.VerifyAsync(password, checkedHash ?? passwords.DummyHash, ct);
        if (user is null) return IdentityErrors.InvalidCredentials;
        // 重算雜湊與比對一樣耗時，在取列鎖之前算好；用不到（密碼剛好被改）就丟掉。
        var upgraded = result is { Valid: true, Upgrade: true } && Argon2Passwords.IsAcceptable(password) ? await passwords.HashAsync(password, ct) : null;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
        // 比對完雜湊後才重讀使用者，若期間被撤銷、停用或改了密碼，就以新狀態拒絕登入。
        await db.Entry(user).ReloadAsync(ct);
        var now = clock.GetUtcNow();
        var locked = user.LockedUntil > now;
        if (!SessionIdentity.Allows(user, "local") || !result.Valid || locked
            || user.PasswordHash != checkedHash || user.SecurityVersion != checkedVersion)
        {
            if (SessionIdentity.Allows(user, "local") && !locked)
            {
                user.FailedLogins++;
                if (user.FailedLogins >= 5) { user.LockedUntil = now.AddMinutes(15); user.FailedLogins = 0; }
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return IdentityErrors.InvalidCredentials;
        }
        user.FailedLogins = 0; user.LockedUntil = null;
        // 只在密碼正確後才回這個代碼，避免從回應差異猜出帳號是否存在或綁定 AD。
        if (DirectoryStale(user, now))
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return IdentityErrors.DirectoryVerificationRequired;
        }
        user.LastSeenAt = now;
        if (upgraded is not null) user.PasswordHash = upgraded;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return user;
    }

    private bool DirectoryStale(User user, DateTimeOffset now)
        => selfService.Value.DirectoryFreshnessDays > 0
            && user.AdEnabled && UserAccounts.DirectoryBound(user)
            && (user.DirectoryVerifiedAt is not { } verified || now - verified > TimeSpan.FromDays(selfService.Value.DirectoryFreshnessDays));
}
