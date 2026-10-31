using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AiNexus.Features.Admin.Identity;

/// <summary>
/// 授權拒絕先寫請求稽核（ADR 0023），再交給 <see cref="ErrorAuthorizationResultHandler"/> 寫出 problem。
/// 放在結果處理而非 requirement handler：每個請求只執行一次（群組與端點的 policy 合併後才到這裡），評估授權本身不帶寫入副作用。
/// </summary>
internal sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly ErrorAuthorizationResultHandler inner = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult is { Forbidden: true, AuthorizationFailure: { } failure })
        {
            // 結果與回應的錯誤代碼相同；沒有 ErrorFailureReason 時框架回 403，用它的預設代碼。FeatureId 由 FeatureAuthorizationHandler 記下。
            var code = failure.FailureReasons.OfType<ErrorFailureReason>().FirstOrDefault()?.Error.Code ?? Problems.DefaultCode(StatusCodes.Status403Forbidden);
            await RequestAudit.DeniedAsync(context, code);
        }
        await inner.HandleAsync(next, context, policy, authorizeResult);
    }
}
