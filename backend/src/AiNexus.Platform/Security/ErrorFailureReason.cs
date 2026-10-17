using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Platform.Security;

/// <summary>授權處理常式的失敗理由；攜帶公開錯誤，讓回應不是單純的 403。</summary>
public sealed class ErrorFailureReason(IAuthorizationHandler handler, Error error) : AuthorizationFailureReason(handler, error.Code)
{
    public Error Error { get; } = error;
}
