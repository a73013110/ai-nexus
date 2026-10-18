using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Authentication;

namespace AiNexus.Features.Admin.Identity.Sessions;

internal sealed class GetAuthSession(IOptions<AdAuthenticationOptions> options, IAntiforgery csrf)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapGet("/session", (HttpContext http, GetAuthSession handler) => handler.Handle(http))
        .AllowAnonymous().WithName("GetAuthSession");

    public Ok<AuthSessionDto> Handle(HttpContext http) => TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
}
