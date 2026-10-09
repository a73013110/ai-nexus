using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Validation;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Identity;

/// <summary>
/// The sign-in handshake under <c>/api/v1/auth</c>. This group has no <see cref="CurrentUserResolution.WithCurrentUser"/>
/// filter: these endpoints establish the session, so they resolve <see cref="CurrentUser"/> themselves where needed.
/// </summary>
public static class AuthEndpoints
{
    public const string CookieScheme = "NexusCookie";

    /// <summary>Endpoint order is the published OpenAPI order.</summary>
    public static void MapNexusAuthentication(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithSafeErrors().WithRequestValidation();
        GetAuthSession.Map(auth);
        WindowsLogin.Map(auth);
        LogIn.Map(auth);
        LogOut.Map(auth);
        StartTestIdentity.Map(auth);
        EndTestIdentity.Map(auth);
    }
}
