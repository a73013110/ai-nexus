using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity.Authentication;

public sealed class LocalAuthenticator(NexusDbContext db, Argon2Passwords passwords, IdentityWriteLock writes)
{
    public async Task<NexusUser> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        string? normalized;
        try { normalized = UserAccounts.Normalize(account); } catch (ApiException) { normalized = null; }
        var user = normalized is null ? null : await db.Users.SingleOrDefaultAsync(x => x.LocalAccount == normalized, ct);
        var checkedHash = user?.PasswordHash;
        var checkedVersion = user?.SecurityVersion;
        var result = await passwords.VerifyAsync(password, checkedHash ?? passwords.DummyHash, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            // Re-read policies after the expensive hash so revocation wins a concurrent login.
            if (user is not null) await db.Entry(user).ReloadAsync(ct);
            var locked = user?.LockedUntil > DateTimeOffset.UtcNow;
            if (user is null || !SessionIdentity.Allows(user, "local") || !result.Valid || locked || user.PasswordHash != checkedHash || user.SecurityVersion != checkedVersion)
            {
                if (user is not null && SessionIdentity.Allows(user, "local") && !locked)
                {
                    user.FailedLogins++;
                    if (user.FailedLogins >= 5) { user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(15); user.FailedLogins = 0; }
                    await db.SaveChangesAsync(ct);
                }
                throw new ApiException(401, "invalid_credentials", "帳號、密碼或登入方式不正確，或帳號暫時無法登入。");
            }
            user.FailedLogins = 0; user.LockedUntil = null; user.LastSeenAt = DateTimeOffset.UtcNow;
            if (result.Upgrade) user.PasswordHash = await passwords.HashAsync(password, ct);
            await db.SaveChangesAsync(ct);
            return user;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(401, "invalid_credentials", "登入設定剛剛更新，請重新登入。");
        }
        finally { writes.Gate.Release(); }
    }
}
