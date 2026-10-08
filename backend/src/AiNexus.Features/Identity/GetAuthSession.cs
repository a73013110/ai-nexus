using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity;

internal static class GetAuthSession
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder auth) => auth
        .MapGet("/session", (HttpContext http, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) => Results.Ok(AuthSession.Describe(http, options.Value, csrf)))
        .AllowAnonymous().WithName("GetAuthSession").Produces<AuthSessionDto>();
}
