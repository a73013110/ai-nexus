using System.Threading.RateLimiting;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.ApiClients;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Ordering;
using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Features.Admin.Identity.Reviews;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Platform.Configuration;

namespace AiNexus.Features.Admin.Identity;

/// <summary>身分模組：Cookie、Negotiate 與外部系統金鑰的驗證、登入速率限制、帳密登入服務的註冊，以及管理端的帳號管理、外部系統與首次管理員。</summary>
public sealed class IdentityModule : IFeatureModule
{
    /// <summary>帳密登入依來源位址的總上限；主要的防護是 <see cref="Authentication.LoginThrottle"/> 依帳號計算失敗次數。</summary>
    public const string LoginRateLimit = "ad-login";
    // 寬鬆到整間公司在 NAT 後面同時上班也碰不到，只擋單一來源大量換帳號猜密碼。
    public const int LoginsPerAddressPerMinute = 120;
    /// <summary>同一帳號在 <see cref="Authentication.LoginThrottle.Window"/> 內允許的帳密失敗次數；比本地帳號的 5 次鎖定寬，鎖定仍由它先觸發。</summary>
    public const int LoginFailuresPerAccount = 10;
    /// <summary>匯入對話框瀏覽 AD 目錄樹與節點人員（每展開一層、每選一個節點各一次）。</summary>
    public const string DirectoryReadRateLimit = "directory-read";
    // 單人屬性另計：以方向鍵逐列看屬性很快就會用掉瀏覽的額度，而且每次只查一個物件，成本遠低於子樹計數。
    public const string DirectoryPersonRateLimit = "directory-person";
    /// <summary>匯入預覽、匯入與從 AD 更新：每次最多 100 人、各自查目錄與寫入。</summary>
    public const string DirectoryWriteRateLimit = "directory-write";
    private const string SessionCookie = "AiNexus.Session";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<AdAuthenticationOptions, AdAuthenticationOptionsValidator>(AdAuthenticationOptions.Section);
        services.AddSettings<AdminBootstrapOptions, AdminBootstrapOptionsValidator>(AdminBootstrapOptions.Section);
        services.AddSettings<SelfServiceOptions, SelfServiceOptionsValidator>(SelfServiceOptions.Section);
        services.AddSettings<SessionLifetimeOptions, SessionLifetimeOptionsValidator>(SessionLifetimeOptions.Section);
        var authentication = services.AddAuthentication("Session")
            // 已有 session cookie 時優先使用 cookie；否則依目錄模式決定：Ldap 走 cookie，Windows 走 Negotiate。
            .AddPolicyScheme("Session", null, options => options.ForwardDefaultSelector = http =>
                http.Request.Cookies.ContainsKey(SessionCookie) || http.RequestServices.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value.Mode == "Ldap"
                    ? AuthEndpoints.CookieScheme : NegotiateDefaults.AuthenticationScheme);
        // Negotiate 只在 Windows 模式註冊：在 IIS 上它要求站台啟用 Windows 驗證，否則啟動即失敗；而 IIS 一啟用 Windows 驗證，
        // 就會在每個 401（包括鎖定的 session_locked）加上 Negotiate 挑戰，讓瀏覽器跳出自己的帳密框。模式改了要重啟。
        if (AdAuthenticationOptions.UsesWindows(builder.Configuration)) authentication.AddNegotiate();
        authentication.AddCookie(AuthEndpoints.CookieScheme, options =>
            {
                options.Cookie.Name = SessionCookie;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = WebSecurity.CookiePolicy(builder.Environment, builder.Configuration);
                // 到期由 SessionTicket 在簽發時指定；滑動到期會被背景輪詢延長，不能開。
                options.SlidingExpiration = false;
                options.Events.OnValidatePrincipal = SessionIdentity.ValidateAsync;
                options.Events.OnRedirectToLogin = context => Problems.WriteAsync(context.HttpContext,
                    SessionLock.IsRejected(context.HttpContext) ? IdentityErrors.SessionLocked : HttpErrors.AuthenticationRequired);
                options.Events.OnRedirectToAccessDenied = context => Problems.WriteAsync(context.HttpContext, HttpErrors.AccessDenied);
            });
        ApiClientAccess.Add(services, authentication);
        services.AddRateLimiter(options => options.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
            http.ClientAddress()?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = LoginsPerAddressPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }))
            .AddPerUserLimit(DirectoryReadRateLimit, 60).AddPerUserLimit(DirectoryPersonRateLimit, 180).AddPerUserLimit(DirectoryWriteRateLimit, 20));
        services.AddSingleton(Argon2Cost.Recommended);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/admin").RequireFeature(FeatureCatalog.IdentityUsers)
            .MapSlice<ManageUserAccounts>()
            .MapSlice<RebindDirectoryAccount>()
            .MapSlice<UnbindDirectoryAccount>()
            .MapSlice<SetUserRoles>()
            .MapSlice<SetUserEmail>()
            .MapSlice<PreviewUserAccess>()
            .MapSlice<ListAccountReviews>()
            .MapSlice<MoveUser>()
            .MapSlice<ResetUserOrder>();
        api.MapGroup("/admin").RequireFeature(FeatureCatalog.IdentityUsersImport)
            .MapSlice<ListDirectoryNodes>()
            .MapSlice<ListDirectoryPeople>()
            .MapSlice<GetDirectoryPerson>()
            .MapSlice<PreviewDirectoryImport>()
            .MapSlice<ImportDirectoryUsers>();
        api.MapGroup("/admin").RequireFeature(FeatureCatalog.IdentityUsersSync)
            .MapSlice<SyncDirectoryUsers>();
        api.MapGroup("/admin/api-clients").RequireFeature(FeatureCatalog.IdentityApiClients)
            .MapSlice<ListApiClients>()
            .MapSlice<CreateApiClient>()
            .MapSlice<UpdateApiClient>()
            .MapSlice<ReissueApiClientKey>()
            .MapSlice<DeleteApiClient>();
        api.MapSlice<VerifyCredentials>();
        api.MapAuthenticationEndpoints();
    }
}
