using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Identity.Sessions;

internal sealed class GetAuthSession(IOptions<AdAuthenticationOptions> options, IAntiforgery csrf)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapGet("/session", (HttpContext http, GetAuthSession handler) => handler.Handle(http))
        .AllowAnonymous().NotAudited("每個頁面載入都會查詢的登入狀態").WithName("GetAuthSession");

    public Ok<AuthSessionDto> Handle(HttpContext http) => TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
}
