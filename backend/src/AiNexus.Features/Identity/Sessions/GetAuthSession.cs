using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Identity.Sessions;

internal static class GetAuthSession
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder auth) => auth
        .MapGet("/session", (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) => Results.Ok(AuthSession.Describe(http, options.Value, csrf)))
        .AllowAnonymous().WithName("GetAuthSession").Produces<AuthSessionDto>();
}
