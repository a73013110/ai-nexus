using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// 以 AD 驗證帳密的唯一入口：網站登入、步升驗證與外部系統的帳密驗證 API 都經過這裡，
/// 輸入容忍（<see cref="UserAccounts.DirectoryLoginName"/>）與失敗計數（<see cref="LoginThrottle"/>）因此只有一份。
/// 帳密錯誤回傳 <c>ad_invalid_credentials</c>；目錄無法連線或未設定服務帳號時拋出 <see cref="ExternalServiceException"/>。
/// </summary>
[Singleton]
public sealed class DirectoryCredentials(IAdAuthenticator directory, LoginThrottle throttle, IOptions<AdAuthenticationOptions> options)
{
    public Task<Result<AdIdentity>> VerifyAsync(HttpResponse response, string account, string password, CancellationToken ct)
    {
        var settings = options.Value;
        return throttle.GuardAsync(response, "ad", account,
            () => directory.AuthenticateAsync(UserAccounts.DirectoryLoginName(account, settings.Domain, settings.AccountSuffixes), password, ct));
    }
}
