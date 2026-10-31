using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Sessions;

namespace AiNexus.Features.Admin.Identity;

/// <summary>
/// <c>/api/v1/auth</c> 下的登入交握端點。這些端點負責建立 session，不宣告 <see cref="ICurrentUser"/>（那會在 session 建立前就要求解析使用者），
/// 需要時自行解析 <see cref="CurrentUser"/>。宣告稽核的端點（選擇身分、測試身分）都需要已登入的身分。
/// </summary>
public static class AuthEndpoints
{
    public const string CookieScheme = "SessionCookie";

    /// <summary>端點註冊順序即 OpenAPI 發布順序。</summary>
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth");
        GetAuthSession.Map(auth);
        WindowsLogin.Map(auth);
        LogIn.Map(auth);
        LogOut.Map(auth);
        SelectIdentity.Map(auth);
        StartTestIdentity.Map(auth);
        EndTestIdentity.Map(auth);
    }
}
