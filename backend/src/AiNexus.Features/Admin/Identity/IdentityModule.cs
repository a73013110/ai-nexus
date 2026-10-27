using System.Threading.RateLimiting;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Ordering;
using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Features.Admin.Identity.Reviews;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Accounts;
using AiNexus.Platform.Configuration;

namespace AiNexus.Features.Admin.Identity;

/// <summary>身分模組：Cookie 與 Negotiate 驗證、登入速率限制、帳密登入服務的註冊，以及管理端的帳號管理與首次管理員。</summary>
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
        services.AddSingleton<IAdAuthenticator, LdapAuthenticator>();
        services.AddSingleton<IAdDirectory, LdapDirectory>();
        services.AddAuthentication("Session")
            // 已有 session cookie 時優先使用 cookie；否則依目錄模式決定：Ldap 走 cookie，Windows 走 Negotiate。
            .AddPolicyScheme("Session", null, options => options.ForwardDefaultSelector = http =>
                http.Request.Cookies.ContainsKey(SessionCookie) || http.RequestServices.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value.Mode == "Ldap"
                    ? AuthEndpoints.CookieScheme : NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate()
            .AddCookie(AuthEndpoints.CookieScheme, options =>
            {
                options.Cookie.Name = SessionCookie;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = WebSecurity.CookiePolicy(builder.Environment, builder.Configuration);
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = SessionIdentity.ValidateAsync;
                options.Events.OnRedirectToLogin = context => Problems.WriteAsync(context.HttpContext, 401, "authentication_required");
                options.Events.OnRedirectToAccessDenied = context => Problems.WriteAsync(context.HttpContext, 403, "access_denied");
            });
        services.AddRateLimiter(options => options.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
            http.ClientAddress()?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = LoginsPerAddressPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }))
            .AddPerUserLimit(DirectoryReadRateLimit, 60).AddPerUserLimit(DirectoryPersonRateLimit, 180).AddPerUserLimit(DirectoryWriteRateLimit, 20));
        services.AddSingleton<IdentityWriteLock>();
        services.AddSingleton<LoginThrottle>();
        services.AddSingleton<DirectoryProfileCache>();
        services.AddScoped<DirectoryAccounts>();
        services.AddScoped<DirectoryProvisioning>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<IRequestUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<IRequestActor>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ErrorAuthorizationResultHandler>();
        services.AddScoped<IActiveUsers, ActiveUsers>();
        services.AddAccountReviewSource<DirectoryAccountReview>();
        services.AddSingleton(Argon2Cost.Recommended);
        services.AddSingleton<Argon2Passwords>();
        services.AddScoped<LocalAuthenticator>();
        services.AddScoped<AuthenticationAudit>();
        services.AddScoped<ISignInGrant, AdminBootstrap>();
        services.AddScoped<VerificationChallenges>();
        services.AddScoped<Reauthentication>();
        services.AddScoped<UserCredentials>();
        services.AddFeaturePolicy(FeatureCatalog.IdentityUsers);
        services.AddFeaturePolicy(FeatureCatalog.IdentityUsersImport);
        services.AddFeaturePolicy(FeatureCatalog.IdentityUsersSync);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.IdentityUsers);
        ManageUserAccounts.MapCreate(admin);
        ManageUserAccounts.MapUpdate(admin);
        ManageUserAccounts.MapDelete(admin);
        RebindDirectoryAccount.Map(admin);
        UnbindDirectoryAccount.Map(admin);
        SetUserRoles.Map(admin);
        SetUserEmail.Map(admin);
        PreviewUserAccess.Map(admin);
        ListAccountReviews.Map(admin);
        MoveUser.Map(admin);
        ResetUserOrder.Map(admin);
        var import = api.MapGroup("/admin").RequireAuthorization(Policies.IdentityUsersImport);
        ListDirectoryNodes.Map(import);
        ListDirectoryPeople.Map(import);
        GetDirectoryPerson.Map(import);
        PreviewDirectoryImport.Map(import);
        ImportDirectoryUsers.Map(import);
        SyncDirectoryUsers.Map(api.MapGroup("/admin").RequireAuthorization(Policies.IdentityUsersSync));
    }

    public static void MapPublicEndpoints(IEndpointRouteBuilder app) => app.MapAuthenticationEndpoints();
}
