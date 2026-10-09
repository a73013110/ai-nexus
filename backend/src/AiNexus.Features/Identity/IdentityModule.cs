using System.Threading.RateLimiting;
using AiNexus.Features.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity;

public sealed class IdentityModule : IFeatureModule
{
    public const string LoginRateLimit = "ad-login";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<AdAuthenticationOptions>().BindConfiguration("AdAuthentication")
            .Validate(x => x.Mode is "Ldap" or "Windows", "AD Mode must be Ldap or Windows.").ValidateOnStart();
        services.AddSingleton<IAdAuthenticator, LdapAuthenticator>();
        services.AddAuthentication("NexusSession")
            // An existing session cookie wins; otherwise the configured directory mode picks cookie (LDAP) or Negotiate.
            .AddPolicyScheme("NexusSession", null, options => options.ForwardDefaultSelector = http =>
                http.Request.Cookies.ContainsKey("Nexus.Session") || http.RequestServices.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value.Mode == "Ldap"
                    ? AuthEndpoints.CookieScheme : NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate()
            .AddCookie(AuthEndpoints.CookieScheme, options =>
            {
                options.Cookie.Name = "Nexus.Session";
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
        services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();
        services.AddScoped<IActiveUsers, ActiveUsers>();
        services.AddSingleton(Argon2Cost.Recommended);
        services.AddSingleton<Argon2Passwords>();
        services.AddScoped<LocalAuthenticator>();
        services.AddScoped<AuthenticationAudit>();
    }

    public static void MapPublicEndpoints(IEndpointRouteBuilder app) => app.MapNexusAuthentication();

    private static Task Problem(HttpContext http, int status, string code)
        => Problems.WriteAsync(http, status, code);
}
