using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AiNexus.Platform.Security;

/// <summary>授權失敗的理由含 <see cref="ErrorFailureReason"/> 時，直接寫出對應的公開 ProblemDetails；其餘情況交給框架預設處理。</summary>
public sealed class ErrorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        => authorizeResult.AuthorizationFailure?.FailureReasons.OfType<ErrorFailureReason>().FirstOrDefault() is { } reason
            ? Problems.WriteAsync(context, Problems.Status(reason.Error.Kind), reason.Error.Code)
            : fallback.HandleAsync(next, context, policy, authorizeResult);
}
