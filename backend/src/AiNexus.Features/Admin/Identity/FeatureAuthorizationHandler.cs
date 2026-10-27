using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Features.Admin.Identity;

// 授權每個請求都從 SQL 重讀，讓撤銷立即生效，不必等 cookie 過期。
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var ct = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        if (!user.IsSuccess) { context.Fail(new ErrorFailureReason(this, user.Error)); return; }
        var grants = await access.ForUserAsync(user.Value.Id, ct);
        if (grants.Features.Any(x => requirement.FeatureIds.Contains(x.Id))) context.Succeed(requirement);
        else context.Fail(new ErrorFailureReason(this, AccessControlErrors.FeatureForbidden));
    }
}
