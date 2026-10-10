using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

internal sealed class GetAuthSession(IOptions<AdAuthenticationOptions> options, IAntiforgery csrf)
{
    public static void Map(RouteGroupBuilder auth) => auth
        .MapGet("/session", (HttpContext http, GetAuthSession handler) => handler.Handle(http))
        .AllowAnonymous().WithName("GetAuthSession");

    public Ok<AuthSessionDto> Handle(HttpContext http) => TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
}
