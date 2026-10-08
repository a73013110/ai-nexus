using Microsoft.AspNetCore.Antiforgery;

namespace AiNexus.Platform.Security;

/// <summary>Header-token CSRF protection for every state-changing API request, including sign-in.</summary>
public static class Csrf
{
    public const string HeaderName = "X-Nexus-CSRF";

    public static IServiceCollection AddCsrfProtection(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
        => services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = "Nexus.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = WebSecurity.CookiePolicy(environment, configuration);
        });

    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        if (http.Request.Path.StartsWithSegments("/api/v1") && http.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        await next(http);
    });
}
