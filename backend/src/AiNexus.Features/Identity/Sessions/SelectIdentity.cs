using System.Security.Claims;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

public sealed record SelectIdentityRequest(string Identity);

internal sealed class SelectIdentityRequestValidator : RequestValidator<SelectIdentityRequest>
{
    public SelectIdentityRequestValidator() => RuleFor(x => x.Identity).NotEmpty().MaximumLength(64);
}

/// <summary>
/// 把 session 切換到帳號提供的另一個身分（<see cref="AuthSession.Identities"/>），保留同一位使用者、登入方式與 cookie 存續時間。
/// 回應附新的 CSRF token。
/// </summary>
internal sealed class SelectIdentity(IOptions<AdAuthenticationOptions> options, IAntiforgery csrf)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapPost("/identity", (SelectIdentityRequest body, HttpContext http, SelectIdentity handler) => handler.HandleAsync(http, body).ToHttpResultAsync())
        .RequireAuthorization().WithName("SelectIdentity");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http, SelectIdentityRequest body)
    {
        if (!AuthSession.Identities(http.User).Any(x => x.Id == body.Identity)) return IdentityErrors.IdentityNotFound;
        var previous = await http.AuthenticateAsync(AuthEndpoints.CookieScheme);
        var claims = http.User.Claims.Where(x => x.Type != SessionIdentity.ActiveIdentity).Append(new(SessionIdentity.ActiveIdentity, body.Identity));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthEndpoints.CookieScheme));
        await http.SignInAsync(AuthEndpoints.CookieScheme, principal, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = previous.Properties?.ExpiresUtc, AllowRefresh = true });
        http.User = principal;
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
