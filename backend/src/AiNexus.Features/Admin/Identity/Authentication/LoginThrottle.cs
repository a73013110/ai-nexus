using AiNexus.Platform.Modules;
using System.Globalization;
using System.Threading.RateLimiting;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// 依帳號計算帳密驗證的失敗次數，擋下針對單一帳號的猜密碼。網站登入、步升驗證與外部系統的帳密驗證共用同一份計數：
/// 換個入口不能多猜幾輪。
/// 來源位址只做寬鬆的總上限（<see cref="IdentityModule.LoginRateLimit"/>）：站台前方有 NAT 時全公司共用一個位址，依位址嚴格限流會讓正常使用者互相擋住。
/// 帳號不存在也照樣計數，回應才不會透露帳號是否存在；計數只在記憶體，站台只有一個 worker（見 IIS 部署）。
/// </summary>
[Singleton]
public sealed class LoginThrottle(IOptions<AdAuthenticationOptions> options) : IDisposable
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly PartitionedRateLimiter<string> failures = PartitionedRateLimiter.Create<string, string>(account =>
        RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = IdentityModule.LoginFailuresPerAccount, Window = Window, QueueLimit = 0, AutoReplenishment = true,
        }));

    /// <summary>
    /// 在失敗計數的保護下嘗試一次帳密驗證。已達上限時不執行 <paramref name="attempt"/>（猜密碼的請求不該再打到 AD，也不會讓 AD 因此鎖住帳號），
    /// 回傳 <c>login_rate_limited</c> 並寫入 <c>Retry-After</c>。只有帳密類的失敗（<see cref="ErrorKind.Unauthenticated"/>）才計數：
    /// 目錄連不上等上游錯誤不是猜密碼，不該把使用者鎖在外面。<paramref name="method"/> 是 <c>ad</c> 或 <c>local</c>：兩種帳號的名稱空間不同，分開計數。
    /// </summary>
    public async Task<Result<T>> GuardAsync<T>(HttpResponse response, string method, string? account, Func<Task<Result<T>>> attempt)
    {
        var key = Key(method, account);
        if (key is not null)
        {
            // 取 0 個 permit 只查詢狀態、不消耗額度；成功的驗證不計入失敗次數。
            using var lease = failures.AttemptAcquire(key, 0);
            if (!lease.IsAcquired)
            {
                var wait = lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : Window;
                response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return IdentityErrors.LoginRateLimited;
            }
        }
        var result = await attempt();
        if (key is not null && !result.IsSuccess && result.Error.Kind == ErrorKind.Unauthenticated) failures.AttemptAcquire(key).Dispose();
        return result;
    }

    /// <summary>
    /// 計數用的帳號鍵。AD 帳號先去掉網域前綴與允許的 UPN 後綴，否則換個寫法（<c>網域\帳號</c>、<c>帳號@網域</c>）就能多猜幾輪。
    /// 空白或超長的帳號、未知的登入方式回傳 null：它們不會通過驗證，只受來源位址的上限約束，也不會佔用計數的記憶體。
    /// </summary>
    private string? Key(string method, string? account)
    {
        if (string.IsNullOrWhiteSpace(account) || account.Length > 256 || method is not ("ad" or "local")) return null;
        var settings = options.Value;
        var name = method == "ad" ? UserAccounts.DirectoryLoginName(account, settings.Domain, settings.AccountSuffixes) : account.Trim();
        return method + ":" + name.ToLowerInvariant();
    }

    public void Dispose() => failures.Dispose();
}
