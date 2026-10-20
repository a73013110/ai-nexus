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
/// 本地帳密驗證。雜湊比對耗時，所以比對時不持鎖；比對完才取得寫入鎖並重讀使用者，讓撤銷與並行登入都以較新的狀態為準。
/// AD 綁定且仍允許 AD 登入的使用者，最近一次 AD 驗證超過 <see cref="SelfServiceOptions.DirectoryFreshnessDays"/> 就拒絕本地密碼：
/// AD 帳號停用（離職）後，自行設定的本地密碼最晚在這個期限後失效。管理員把 AD 登入關掉的帳號是明確的本地專用帳號，不套用。
/// </summary>
public sealed class LocalAuthenticator(AppDbContext db, Argon2Passwords passwords, IdentityWriteLock writes, IOptions<SelfServiceOptions> selfService, TimeProvider clock)
{
    public async Task<Result<User>> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        var normalized = UserAccounts.IsValid(account) ? UserAccounts.Normalize(account) : null;
        var user = normalized is null ? null : await db.Users.SingleOrDefaultAsync(x => x.LocalAccount == normalized, ct);
        var checkedHash = user?.PasswordHash;
        var checkedVersion = user?.SecurityVersion;
        var result = await passwords.VerifyAsync(password, checkedHash ?? passwords.DummyHash, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            // 比對完雜湊後才重讀使用者，若期間被撤銷、停用或改了密碼，就以新狀態拒絕登入。
            if (user is not null) await db.Entry(user).ReloadAsync(ct);
            var now = clock.GetUtcNow();
            var locked = user?.LockedUntil > now;
            if (user is null || !SessionIdentity.Allows(user, "local") || !result.Valid || locked
                || user.PasswordHash != checkedHash || user.SecurityVersion != checkedVersion)
            {
                if (user is not null && SessionIdentity.Allows(user, "local") && !locked)
                {
                    user.FailedLogins++;
                    if (user.FailedLogins >= 5) { user.LockedUntil = now.AddMinutes(15); user.FailedLogins = 0; }
                    await db.SaveChangesAsync(ct);
                }
                return IdentityErrors.InvalidCredentials;
            }
            user.FailedLogins = 0; user.LockedUntil = null;
            // 只在密碼正確後才回這個代碼，避免從回應差異猜出帳號是否存在或綁定 AD。
            if (DirectoryStale(user, now))
            {
                await db.SaveChangesAsync(ct);
                return IdentityErrors.DirectoryVerificationRequired;
            }
            user.LastSeenAt = now;
            if (result.Upgrade && Argon2Passwords.IsAcceptable(password)) user.PasswordHash = await passwords.HashAsync(password, ct);
            await db.SaveChangesAsync(ct);
            return user;
        }
        catch (DbUpdateConcurrencyException)
        {
            // 比對雜湊期間登入設定被改動（樂觀併發衝突），一律視為帳密錯誤，不透露原因。
            return IdentityErrors.InvalidCredentials;
        }
        finally { writes.Gate.Release(); }
    }

    private bool DirectoryStale(User user, DateTimeOffset now)
        => user.AdEnabled && !user.Sid.StartsWith("managed:", StringComparison.Ordinal)
            && (user.DirectoryVerifiedAt is not { } verified || now - verified > TimeSpan.FromDays(selfService.Value.DirectoryFreshnessDays));
}
