using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>帳號與權限分頁的安全資料：信箱與來源、本地登入、可用的步升方式與步升期限。</summary>
internal sealed class GetAccountSecurity(AccountSecurity security)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings/account", (HttpContext http, ICurrentUser user, GetAccountSecurity handler) => TypedResults.Ok(handler.Handle(http, user.User)))
        .WithName("GetAccountSecurity");

    public AccountSecurityDto Handle(HttpContext http, User user) => security.Describe(http, user);
}
