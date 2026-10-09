using AiNexus.Features.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Features.Identity;

// Grants are read from SQL once per request so revocation does not wait for a cookie to expire.
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        if (requirement.FeatureIds.Contains(FeatureIds.Admin) && context.User.HasClaim(x => x.Type == SessionIdentity.ActorId) &&
            http.HttpContext?.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            throw new ApiException(403, "test_admin_read_only", "測試身分期間只能檢視管理功能；請先返回管理者再異動帳號與授權。");
        var ct = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        var grants = await access.ForUserAsync(user.Id, ct);
        if (grants.Features.Any(x => requirement.FeatureIds.Contains(x.Id))) context.Succeed(requirement);
        else throw new ApiException(403, "feature_forbidden", "你的角色目前沒有使用此功能的權限，請聯絡管理員。");
    }
}
