using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AiNexus.Platform.Security;

/// <summary>Writes the first <see cref="ErrorFailureReason"/> as the problem response; other outcomes keep the framework behavior.</summary>
public sealed class ErrorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        => authorizeResult.AuthorizationFailure?.FailureReasons.OfType<ErrorFailureReason>().FirstOrDefault() is { } reason
            ? Problems.WriteAsync(context, Problems.Status(reason.Error.Kind), reason.Error.Code)
            : fallback.HandleAsync(next, context, policy, authorizeResult);
}
