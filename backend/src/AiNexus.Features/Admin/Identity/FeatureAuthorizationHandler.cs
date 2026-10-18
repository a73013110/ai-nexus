using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using AiNexus.Features.Admin.Identity.Sessions;

namespace AiNexus.Features.Admin.Identity;

// 授權每個請求都從 SQL 重讀，讓撤銷立即生效，不必等 cookie 過期。
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        // 測試身分可讀取管理 API（存取管理與平台營運），但絕不可變更。
        var guardsAdminApi = requirement.FeatureIds.Contains(FeatureIds.Admin) || requirement.FeatureIds.Contains(FeatureIds.Operations);
        var isTestIdentity = context.User.HasClaim(x => x.Type == SessionIdentity.ActorId);
        var isMutation = http.HttpContext?.Request.Method is not ("GET" or "HEAD" or "OPTIONS");
        if (guardsAdminApi && isTestIdentity && isMutation)
        {
            context.Fail(new ErrorFailureReason(this, IdentityErrors.TestAdminReadOnly));
            return;
        }
        var ct = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        if (!user.IsSuccess) { context.Fail(new ErrorFailureReason(this, user.Error)); return; }
        var grants = await access.ForUserAsync(user.Value.Id, ct);
        if (grants.Features.Any(x => requirement.FeatureIds.Contains(x.Id))) context.Succeed(requirement);
        else context.Fail(new ErrorFailureReason(this, AccessControlErrors.FeatureForbidden));
    }
}
