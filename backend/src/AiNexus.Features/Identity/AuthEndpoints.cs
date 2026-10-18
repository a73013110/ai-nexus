using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Validation;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Identity;

/// <summary>
/// <c>/api/v1/auth</c> 下的登入交握端點。此群組不掛 <see cref="CurrentUserResolution.WithCurrentUser"/> 過濾器：這些端點負責建立 session，
/// 需要時自行解析 <see cref="CurrentUser"/>。
/// </summary>
public static class AuthEndpoints
{
    public const string CookieScheme = "SessionCookie";

    /// <summary>端點註冊順序即 OpenAPI 發布順序。</summary>
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithSafeErrors().WithRequestValidation();
        GetAuthSession.Map(auth);
        WindowsLogin.Map(auth);
        LogIn.Map(auth);
        LogOut.Map(auth);
        SelectIdentity.Map(auth);
        StartTestIdentity.Map(auth);
        EndTestIdentity.Map(auth);
    }
}
