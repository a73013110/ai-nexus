using System.Text;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity;

public sealed record UserAuthenticationDto(bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, bool HasLocalPassword);

public static class UserAccounts
{
    /// <summary>The directory account without domain prefix or UPN suffix.</summary>
    public static string AccountName(string account) => account.Trim().Split('\\').Last().Split('@')[0];

    public static string? Normalize(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return null;
        var value = account.Trim().Normalize(NormalizationForm.FormC);
        if (!Matches(value))
            throw new ApiException(400, "invalid_account", "登入帳號需為 1–64 個字元，只能使用字母、數字、點、底線與連字號。AD 請填目錄中的帳號，不含網域。");
        return value.ToUpperInvariant();
    }

    /// <summary>Whether <see cref="Normalize"/> accepts <paramref name="account"/>; an empty account means "not set".</summary>
    internal static bool IsValid(string? account) => string.IsNullOrWhiteSpace(account) || Matches(account.Trim().Normalize(NormalizationForm.FormC));

    private static bool Matches(string value) => Regex.IsMatch(value, @"^[\p{L}\p{N}][\p{L}\p{N}._-]{0,63}$");

    public static UserAuthenticationDto Authentication(NexusUser user) => new(user.AdEnabled, user.LocalEnabled, user.AdAccount, user.LocalAccount, user.PasswordHash is not null);
}

public sealed class LocalAuthenticator(NexusDbContext db, Argon2Passwords passwords, IdentityWriteLock writes)
{
    public async Task<NexusUser> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        string? normalized;
        try { normalized = UserAccounts.Normalize(account); } catch (ApiException) { normalized = null; }
        var user = normalized is null ? null : await db.Users.SingleOrDefaultAsync(x => x.LocalAccount == normalized, ct);
        var checkedHash = user?.PasswordHash;
        var checkedVersion = user?.SecurityVersion;
        var result = await passwords.VerifyAsync(password, checkedHash ?? Argon2Passwords.DummyHash, ct);
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
