using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Features.Admin.Identity;

// 授權每個請求都從 SQL 重讀，讓撤銷立即生效，不必等 cookie 過期。
// 記下授權或被拒絕的功能（稽核與功能使用量依它統計）；拒絕的稽核由 AuditingAuthorizationResultHandler 寫（ADR 0023）。
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var request = http.HttpContext;
        var ct = request?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        if (!user.IsSuccess) { context.Fail(new ErrorFailureReason(this, user.Error)); return; }
        var grants = await access.ForUserAsync(user.Value.Id, ct);
        var granted = requirement.FeatureIds.FirstOrDefault(id => grants.Features.Any(x => x.Id == id));
        // 拒絕時記下被要求的功能，拒絕的稽核才看得出擋在哪裡：明確 Fail 時框架不會列出失敗的 requirement。
        if (request is not null) AuditContextSource.Authorize(request, granted ?? requirement.FeatureIds[0]);
        if (granted is null) context.Fail(new ErrorFailureReason(this, AccessControlErrors.FeatureForbidden));
        else context.Succeed(requirement);
    }
}
