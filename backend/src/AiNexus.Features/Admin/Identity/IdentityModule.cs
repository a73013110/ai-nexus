using System.Threading.RateLimiting;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Configuration;

namespace AiNexus.Features.Admin.Identity;

/// <summary>身分模組：Cookie 與 Negotiate 驗證、登入速率限制、帳密登入服務的註冊，以及管理端的帳號管理與首次管理員。</summary>
public sealed class IdentityModule : IFeatureModule
{
    public const string LoginRateLimit = "ad-login";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<AdAuthenticationOptions, AdAuthenticationOptionsValidator>(AdAuthenticationOptions.Section);
        services.AddSettings<AdminBootstrapOptions, AdminBootstrapOptionsValidator>(AdminBootstrapOptions.Section);
        services.AddSingleton<IAdAuthenticator, LdapAuthenticator>();
        services.AddAuthentication("Session")
            // 已有 session cookie 時優先使用 cookie；否則依目錄模式決定：Ldap 走 cookie，Windows 走 Negotiate。
            .AddPolicyScheme("Session", null, options => options.ForwardDefaultSelector = http =>
                http.Request.Cookies.ContainsKey("AiNexus.Session") || http.RequestServices.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value.Mode == "Ldap"
                    ? AuthEndpoints.CookieScheme : NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate()
            .AddCookie(AuthEndpoints.CookieScheme, options =>
            {
                options.Cookie.Name = "AiNexus.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = WebSecurity.CookiePolicy(builder.Environment, builder.Configuration);
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = SessionIdentity.ValidateAsync;
                options.Events.OnRedirectToLogin = context => Problem(context.HttpContext, 401, "authentication_required");
                options.Events.OnRedirectToAccessDenied = context => Problem(context.HttpContext, 403, "access_denied");
            });
        services.AddRateLimiter(options => options.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
        services.AddSingleton<IdentityWriteLock>();
        services.AddSingleton<DisplayNameCache>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<IRequestUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<IRequestActor>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ErrorAuthorizationResultHandler>();
        services.AddScoped<IActiveUsers, ActiveUsers>();
        services.AddSingleton(Argon2Cost.Recommended);
        services.AddSingleton<Argon2Passwords>();
        services.AddScoped<LocalAuthenticator>();
        services.AddScoped<AuthenticationAudit>();
        services.AddScoped<ISignInGrant, AdminBootstrap>();
        services.AddFeaturePolicy(FeatureCatalog.IdentityUsers);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.IdentityUsers);
        ManageUserAccounts.MapCreate(admin);
        ManageUserAccounts.MapUpdate(admin);
        ManageUserAccounts.MapDelete(admin);
        SetUserRoles.Map(admin);
        PreviewUserAccess.Map(admin);
    }

    public static void MapPublicEndpoints(IEndpointRouteBuilder app) => app.MapAuthenticationEndpoints();

    private static Task Problem(HttpContext http, int status, string code)
        => Problems.WriteAsync(http, status, code);
}
