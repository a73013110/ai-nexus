using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AiNexus.Platform.Security;

/// <summary>自訂授權結果處理器：若授權失敗原因包含 <see cref="ErrorFailureReason"/>，則直接寫入自訂錯誤 ProblemDetails 回應；其餘狀況保留框架預設行為。</summary>
public sealed class ErrorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        => authorizeResult.AuthorizationFailure?.FailureReasons.OfType<ErrorFailureReason>().FirstOrDefault() is { } reason
            ? Problems.WriteAsync(context, Problems.Status(reason.Error.Kind), reason.Error.Code)
            : fallback.HandleAsync(next, context, policy, authorizeResult);
}
