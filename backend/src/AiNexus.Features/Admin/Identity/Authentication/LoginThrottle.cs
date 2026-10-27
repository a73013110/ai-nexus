using System.Threading.RateLimiting;
using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// 依帳號計算帳密登入的失敗次數，擋下針對單一帳號的猜密碼。
/// 來源位址只做寬鬆的總上限（<see cref="IdentityModule.LoginRateLimit"/>）：站台前方有 NAT 時全公司共用一個位址，依位址嚴格限流會讓正常使用者互相擋住。
/// 帳號不存在也照樣計數，回應才不會透露帳號是否存在；計數只在記憶體，站台只有一個 worker（見 IIS 部署）。
/// </summary>
internal sealed class LoginThrottle : IDisposable
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly PartitionedRateLimiter<string> failures = PartitionedRateLimiter.Create<string, string>(account =>
        RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = IdentityModule.LoginFailuresPerAccount, Window = Window, QueueLimit = 0, AutoReplenishment = true,
        }));

    /// <summary>
    /// 計數用的帳號鍵。AD 帳號先去掉網域前綴與允許的 UPN 後綴，否則換個寫法（<c>網域\帳號</c>、<c>帳號@網域</c>）就能多猜幾輪。
    /// 空白或超長的帳號、未知的登入方式回傳 null：它們不會通過驗證，只受來源位址的上限約束，也不會佔用計數的記憶體。
    /// </summary>
    public static string? Key(AdLoginRequest body, AdAuthenticationOptions options)
    {
        if (string.IsNullOrWhiteSpace(body.Account) || body.Account.Length > 256 || body.Method is not ("ad" or "local")) return null;
        var account = body.Method == "ad" ? UserAccounts.DirectoryLoginName(body.Account, options.Domain, options.AccountSuffixes) : body.Account.Trim();
        return body.Method + ":" + account.ToLowerInvariant();
    }

    /// <summary>此帳號已達失敗上限時，回傳還要等多久；未達上限回傳 null。</summary>
    public TimeSpan? RetryAfter(string? key)
    {
        if (key is null) return null;
        // 取 0 個 permit 只查詢狀態、不消耗額度；成功的登入不計入失敗次數。
        using var lease = failures.AttemptAcquire(key, 0);
        if (lease.IsAcquired) return null;
        return lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : Window;
    }

    public void Failed(string? key)
    {
        if (key is not null) failures.AttemptAcquire(key).Dispose();
    }

    public void Dispose() => failures.Dispose();
}
