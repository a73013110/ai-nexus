using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Platform.Account.Security;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Account;

/// <summary>登入者本人的個人資料、偏好、設定與用量，以及自助維護的通知信箱與本地登入。</summary>
public sealed class AccountModule : IFeatureModule
{
    /// <summary>寄驗證碼、步升與確認信箱：每人每分鐘 10 次。寄信量另由資料庫的每小時發碼上限限制，猜碼另由每組驗證碼的次數上限限制。</summary>
    public const string VerificationRateLimit = "account-verification";
    /// <summary>啟用、變更、停用本地登入：每人每分鐘 5 次。</summary>
    public const string CredentialsRateLimit = "account-credentials";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<PersonalUsage>();
        builder.Services.AddScoped<AccountSecurity>();
        builder.Services.AddDomainEventHandler<UserEmailChanged, NotifyAdministeredEmailChange>();
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(VerificationRateLimit, 10).AddPerUserLimit(CredentialsRateLimit, 5));
    }

    /// <summary>端點註冊順序即公開的 OpenAPI 順序，調整會改變對外文件。</summary>
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        GetMe.Map(api);
        UpdatePreferences.Map(api);
        GetUserSettings.Map(api);
        SaveUserSettings.Map(api);
        GetPersonalUsage.Map(api);
        GetAccountSecurity.Map(api);
        SendVerificationCode.Map(api);
        GetVerificationCode.Map(api);
        Reauthenticate.Map(api);
        RequestEmailChange.Map(api);
        ConfirmEmailChange.Map(api);
        SetLocalLogin.Map(api);
        DisableLocalLogin.Map(api);
    }
}
