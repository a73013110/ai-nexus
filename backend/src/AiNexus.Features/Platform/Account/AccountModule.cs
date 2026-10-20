using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Mail;
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
        AddMailSamples(builder.Services);
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(VerificationRateLimit, 10).AddPerUserLimit(CredentialsRateLimit, 5));
    }

    /// <summary>登記本模組的系統信供管理頁寄信測試；參數依模型建構子順序宣告，範例值是實際流程會出現的樣子，人名與信箱用虛構值。</summary>
    private static void AddMailSamples(IServiceCollection services)
    {
        static VerificationCodeMailModel Code(MailSampleValues x) => new(
            x.Text(nameof(VerificationCodeMailModel.DisplayName), "收件者顯示名稱", MailSampleContext.ExampleName),
            x.Text(nameof(VerificationCodeMailModel.Code), "驗證碼（實際流程由伺服器產生；測試請勿填真的驗證碼）", "123456"),
            x.Number(nameof(VerificationCodeMailModel.Minutes), "有效分鐘數（實際流程取 Identity:SelfService:CodeMinutes）", 15));
        services.AddMailSample(new EmailChangeCodeMail(), "通知信箱驗證碼", "使用者設定通知信箱時寄到新信箱的驗證碼；敏感信，寄出後內文即刪除。", Code);
        services.AddMailSample(new ReauthenticationCodeMail(), "步升驗證碼", "變更帳號安全設定前寄到已驗證信箱的驗證碼；敏感信，寄出後內文即刪除。", Code);
        services.AddMailSample(new AccountNoticeMail(), "帳號安全通知", "通知信箱或本地登入變更後，寄到變更前已驗證的信箱。",
            x => new AccountNoticeMailModel(
                x.Text(nameof(AccountNoticeMailModel.DisplayName), "收件者顯示名稱", MailSampleContext.ExampleName),
                x.Text(nameof(AccountNoticeMailModel.Change), "接在「你的帳號剛剛」後面的變更描述", "變更了通知信箱"),
                x.OptionalText(nameof(AccountNoticeMailModel.Detail), "補充說明，只放遮罩後的值；空白＝不顯示", $"新的通知信箱：{MailSampleContext.ExampleMaskedEmail}"),
                x.Flag(nameof(AccountNoticeMailModel.ByAdministrator), "由管理員變更（結語改為請本人確認）", false)));
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
