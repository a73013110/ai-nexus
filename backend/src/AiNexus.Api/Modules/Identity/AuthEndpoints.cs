using System.Security.Claims;
using AiNexus.BuildingBlocks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Identity;

public sealed record AuthSessionDto(string Mode, bool Authenticated, bool Configured, string? Account, string? DisplayName, string CsrfToken);
public sealed record AdLoginRequest(string Account, string Password);

public static class AuthEndpoints
{
    public const string CookieScheme = "NexusCookie";
    public static void MapNexusAuthentication(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth");
        auth.MapGet("/session", (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) => Results.Ok(Session(http, options.Value, csrf)))
            .AllowAnonymous().WithName("GetAuthSession").Produces<AuthSessionDto>();
        auth.MapPost("/login", async (AdLoginRequest body, HttpContext http, IAdAuthenticator directory, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf, CancellationToken ct) =>
        {
            if (options.Value.Mode != "Ldap") throw new ApiException(400, "authentication_mode", "此工作台使用 Windows 整合驗證。");
            var identity = await directory.AuthenticateAsync(body.Account.Trim(), body.Password, ct);
            var claims = new[] { new Claim(ClaimTypes.PrimarySid, identity.Sid), new Claim(ClaimTypes.Name, identity.Account), new Claim("display_name", identity.DisplayName) };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme));
            await http.SignInAsync(CookieScheme, principal, new AuthenticationProperties { IsPersistent = false });
            http.User = principal;
            return Results.Ok(Session(http, options.Value, csrf));
        }).AllowAnonymous().RequireRateLimiting("ad-login").WithName("AdLogin").Produces<AuthSessionDto>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);
        auth.MapPost("/logout", async (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) =>
        {
            if (options.Value.Mode != "Ldap") throw new ApiException(400, "authentication_mode", "Windows 整合驗證由公司工作階段管理。");
            await http.SignOutAsync(CookieScheme);
            http.User = new ClaimsPrincipal(new ClaimsIdentity());
            return Results.Ok(Session(http, options.Value, csrf));
        }).RequireAuthorization().WithName("AdLogout").Produces<AuthSessionDto>();
    }

    private static AuthSessionDto Session(HttpContext http, AdAuthenticationOptions options, IAntiforgery csrf)
    {
        http.Response.Headers.CacheControl = "no-store";
        return new(options.Mode, http.User.Identity?.IsAuthenticated == true, options.Mode == "Windows" || options.Configured, http.User.Identity?.Name, http.User.FindFirstValue("display_name"), csrf.GetAndStoreTokens(http).RequestToken!);
    }
}
