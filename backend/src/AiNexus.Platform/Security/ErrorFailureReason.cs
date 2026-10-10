using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Platform.Security;

/// <summary>An authorization handler's reason for failing, answered with this public error instead of a bare 403.</summary>
public sealed class ErrorFailureReason(IAuthorizationHandler handler, Error error) : AuthorizationFailureReason(handler, error.Code)
{
    public Error Error { get; } = error;
}
