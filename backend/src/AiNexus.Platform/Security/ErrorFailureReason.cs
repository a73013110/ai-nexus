using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Platform.Security;

/// <summary>授權失敗的理由，攜帶公開錯誤代碼，讓回應不只是單純的 403。</summary>
public sealed class ErrorFailureReason(IAuthorizationHandler handler, Error error) : AuthorizationFailureReason(handler, error.Code)
{
    public Error Error { get; } = error;
}
