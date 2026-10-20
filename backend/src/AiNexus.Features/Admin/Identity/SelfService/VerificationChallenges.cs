using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.SelfService;

/// <summary>剛發出的驗證碼。<paramref name="Code"/> 只交給寄信範本，不可回傳給前端或寫入日誌；<paramref name="Superseded"/> 是被取代的舊碼所寄的信（呼叫端可取消）。</summary>
public sealed record IssuedChallenge(VerificationChallenge Challenge, string Code, IReadOnlyList<Guid> Superseded);

/// <summary>
/// 一次性 6 位數驗證碼（變更信箱、步升驗證）。只保存 HMAC 雜湊；到期、錯誤次數上限、成功使用或被新碼取代後即結束。
/// Identity 不認識 Mail：寄信由呼叫端（Account）在同一交易入列，再把信件 Id 寫回 <see cref="VerificationChallenge.MailMessageId"/>。
/// </summary>
public sealed class VerificationChallenges(AppDbContext db, IOptions<SelfServiceOptions> options, TimeProvider clock)
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>
    /// 發出新碼：先檢查每小時上限與同用途的冷卻時間，再立即撤銷同用途未結束的舊碼（直接寫入資料庫；filtered unique index 保證只剩一組有效）。
    /// 新列只加入 <see cref="AppDbContext"/>，由呼叫端與寄信一起 <c>SaveChanges</c>；並行發碼時唯一索引會讓其中一方儲存失敗。
    /// </summary>
    public async Task<Result<IssuedChallenge>> IssueAsync(Guid userId, string purpose, string? target, CancellationToken ct)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var challenges = db.Set<VerificationChallenge>();
        var recent = await challenges.AsNoTracking().Where(x => x.UserId == userId && x.CreatedAt > now.AddHours(-1))
            .Select(x => new { x.Purpose, x.CreatedAt }).ToListAsync(ct);
        if (recent.Count >= settings.CodesPerHour) return IdentityErrors.VerificationRateLimited;
        if (recent.Any(x => x.Purpose == purpose && x.CreatedAt > now.AddSeconds(-settings.CodeCooldownSeconds))) return IdentityErrors.VerificationCooldown;
        var open = challenges.Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAt == null && x.RevokedAt == null);
        var superseded = await open.Where(x => x.MailMessageId != null).Select(x => x.MailMessageId!.Value).ToListAsync(ct);
        await open.ExecuteUpdateAsync(p => p.SetProperty(x => x.RevokedAt, now), ct);
        // 量極小，發新碼時順手清掉 30 天前的紀錄，不另開清理工作。
        await challenges.Where(x => x.UserId == userId && x.CreatedAt < now - Retention).ExecuteDeleteAsync(ct);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var challenge = new VerificationChallenge
        {
            Id = Guid.NewGuid(), UserId = userId, Purpose = purpose, Target = target, CreatedAt = now, ExpiresAt = now.AddMinutes(settings.CodeMinutes),
        };
        challenge.CodeHash = Hash(challenge.Id, code);
        challenges.Add(challenge);
        return new IssuedChallenge(challenge, code, superseded);
    }

    /// <summary>使用者自己的驗證碼（顯示到期時間、剩餘次數與寄送狀態）；別人的或不存在時為 null。</summary>
    public Task<VerificationChallenge?> FindAsync(Guid userId, Guid id, CancellationToken ct)
        => db.Set<VerificationChallenge>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public int AttemptsLeft(VerificationChallenge challenge) => Math.Max(0, options.Value.CodeAttempts - challenge.Attempts);

    /// <summary>仍可輸入：未使用、未撤銷、未到期且未達錯誤上限。</summary>
    public bool Open(VerificationChallenge challenge)
        => challenge.ConsumedAt is null && challenge.RevokedAt is null && challenge.ExpiresAt > clock.GetUtcNow() && challenge.Attempts < options.Value.CodeAttempts;

    /// <summary>
    /// 比對驗證碼（固定時間）。錯誤時以條件式 UPDATE 累加次數，達上限即撤銷；正確時以條件式 UPDATE 標記已使用，
    /// 只有影響一列才算成功，所以並行送出同一組碼只有一次通過。兩種寫入都立即提交，不等呼叫端儲存。
    /// </summary>
    public async Task<Result<VerificationChallenge>> VerifyAsync(Guid userId, Guid id, string purpose, string? code, CancellationToken ct)
    {
        var challenge = await FindAsync(userId, id, ct);
        if (challenge is null || challenge.Purpose != purpose || !Open(challenge)) return IdentityErrors.VerificationExpired;
        var max = options.Value.CodeAttempts;
        var now = clock.GetUtcNow();
        var rows = db.Set<VerificationChallenge>().Where(x => x.Id == id && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now && x.Attempts < max);
        var candidate = code?.Trim() ?? "";
        if (candidate.Length != 6 || !candidate.All(char.IsAsciiDigit) || !CryptographicOperations.FixedTimeEquals(Hash(id, candidate), challenge.CodeHash))
        {
            var counted = await rows.ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                .SetProperty(x => x.RevokedAt, x => x.Attempts + 1 >= max ? now : x.RevokedAt), ct);
            return counted == 1 && challenge.Attempts + 1 < max ? IdentityErrors.VerificationCodeInvalid : IdentityErrors.VerificationExpired;
        }
        if (await rows.ExecuteUpdateAsync(p => p.SetProperty(x => x.ConsumedAt, now), ct) != 1) return IdentityErrors.VerificationExpired;
        challenge.ConsumedAt = now;
        return challenge;
    }

    /// <summary>
    /// HMAC-SHA256，金鑰是驗證碼列自己的 Id。驗證碼 15 分鐘失效又有次數上限，資料庫外洩後離線暴力破解沒有價值，所以不另設秘密金鑰；
    /// 用 HMAC 而非單純雜湊，是讓同一個 6 位數在不同列有不同的雜湊。
    /// </summary>
    public static byte[] Hash(Guid id, string code) => HMACSHA256.HashData(id.ToByteArray(), Encoding.UTF8.GetBytes(code));
}
