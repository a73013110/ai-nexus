using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Identity.Authentication;

public sealed class LocalAuthenticator(NexusDbContext db, Argon2Passwords passwords, IdentityWriteLock writes, TimeProvider clock)
{
    public async Task<Result<NexusUser>> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        var normalized = UserAccounts.IsValid(account) ? UserAccounts.Normalize(account) : null;
        var user = normalized is null ? null : await db.Users.SingleOrDefaultAsync(x => x.LocalAccount == normalized, ct);
        var checkedHash = user?.PasswordHash;
        var checkedVersion = user?.SecurityVersion;
        var result = await passwords.VerifyAsync(password, checkedHash ?? passwords.DummyHash, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            // Re-read policies after the expensive hash so revocation wins a concurrent login.
            if (user is not null) await db.Entry(user).ReloadAsync(ct);
            var now = clock.GetUtcNow();
            var locked = user?.LockedUntil > now;
            if (user is null || !SessionIdentity.Allows(user, "local") || !result.Valid || locked || user.PasswordHash != checkedHash || user.SecurityVersion != checkedVersion)
            {
                if (user is not null && SessionIdentity.Allows(user, "local") && !locked)
                {
                    user.FailedLogins++;
                    if (user.FailedLogins >= 5) { user.LockedUntil = now.AddMinutes(15); user.FailedLogins = 0; }
                    await db.SaveChangesAsync(ct);
                }
                return IdentityErrors.InvalidCredentials;
            }
            user.FailedLogins = 0; user.LockedUntil = null; user.LastSeenAt = now;
            if (result.Upgrade && Argon2Passwords.IsAcceptable(password)) user.PasswordHash = await passwords.HashAsync(password, ct);
            await db.SaveChangesAsync(ct);
            return user;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Sign-in settings changed while the hash was checked.
            return IdentityErrors.InvalidCredentials;
        }
        finally { writes.Gate.Release(); }
    }
}
