using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// 剛通過 AD 驗證（Windows 整合驗證或 LDAP 登入）的 SID 對應到平台使用者：必要時建立、綁定或同步，並套用 <see cref="ISignInGrant"/>。
/// 建立與同步的規則在 <see cref="DirectoryAccounts"/>，與管理員從 AD 匯入、更新共用。
/// </summary>
public sealed class DirectorySignIn(AppDbContext db, IdentityWriteLock writeLock, IEnumerable<ISignInGrant> grants, DirectoryProfileCache profiles,
    IDirectoryProfileLookup directory, DirectoryAccounts accounts, TimeProvider clock, AuditTrail audit)
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// <paramref name="known"/> 是驗證端已帶來的目錄資料（LDAP 登入）；為 null 時只在需要同步時才查目錄。
    /// 回傳的使用者可能未被追蹤，由呼叫端決定是否 Attach。
    /// </summary>
    public async Task<Result<User>> ResolveAsync(string sid, string account, DirectoryProfile? known, CancellationToken ct)
    {
        // Windows 整合驗證每個請求都走到這裡，所以常態只查這一列：不查目錄、不取鎖。
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Sid == sid, ct);
        var profile = known;
        if (existing is not null)
        {
            if (!SessionIdentity.Allows(existing, "ad")) return IdentityErrors.LoginMethodDisabled;
            var now = clock.GetUtcNow();
            var due = now - existing.LastSeenAt > SyncInterval || existing.DirectoryVerifiedAt is null;
            profile ??= due ? Lookup(sid, account) : null;
            // 只有欄位要改（姓名、信箱、帳號改名）、要套用授權才取鎖；單純刷新驗證時間用一句 UPDATE，不與其他登入排隊。
            var current = (profile is null || !DirectoryAccounts.Differs(existing, profile))
                && !(due && DirectoryAccounts.RenamedAdAccount(existing, account) is not null);
            if (current && !await PendingGrantAsync(existing, ct))
            {
                if (due) await TouchAsync(existing, now, ct);
                return existing;
            }
        }
        else profile ??= Lookup(sid, account);
        return await WriteAsync(sid, account, profile, ct);
    }

    /// <summary>
    /// 綁定是依帳號跨 SID 進行，且本地登入與管理員匯入寫的也是同一批資料列，所以共用全域寫入鎖，並在鎖內重新比對。
    /// </summary>
    private async Task<Result<User>> WriteAsync(string sid, string account, DirectoryProfile? profile, CancellationToken ct)
    {
        await writeLock.Gate.WaitAsync(ct);
        try
        {
            var match = await accounts.MatchAsync(sid, account, ct);
            if (!match.IsSuccess) return match.Error;
            var user = match.Value.Existing;
            if (user is not null && !SessionIdentity.Allows(user, "ad")) return IdentityErrors.LoginMethodDisabled;
            var now = clock.GetUtcNow();
            if (user is null)
            {
                // 目錄資料未知時先用帳號名建立，之後的定期同步會補上姓名與信箱。
                user = await accounts.CreateAsync(Guid.NewGuid(), sid, account, profile ?? new(UserAccounts.AccountName(account), null), now, ct);
                audit.Record(IdentityAudit.AccountCreated, user.Id, user.Id, user.Id);
            }
            else
            {
                DirectoryAccounts.Apply(user, await accounts.ChangesAsync(user, account, profile, ct), now);
                user.LastSeenAt = now;
            }
            // 走到這裡的請求都剛通過 AD（LDAP bind 或 Kerberos／NTLM），是本地密碼新鮮度規則的依據；管理員匯入的帳號則留空，直到本人登入。
            user.DirectoryVerifiedAt = now;
            await db.SaveChangesAsync(ct);
            foreach (var grant in grants) await grant.ApplyAsync(user, ct);
            return user;
        }
        finally { writeLock.Gate.Release(); }
    }

    private async Task TouchAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        await db.Users.Where(x => x.Id == user.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, now).SetProperty(x => x.DirectoryVerifiedAt, now), ct);
        user.LastSeenAt = now; user.DirectoryVerifiedAt = now;
    }

    private async Task<bool> PendingGrantAsync(User user, CancellationToken ct)
    {
        foreach (var grant in grants)
            if (await grant.PendingAsync(user, ct)) return true;
        return false;
    }

    private DirectoryProfile? Lookup(string sid, string account) => profiles.GetOrAdd(sid, () => directory.Find(sid, account));
}
