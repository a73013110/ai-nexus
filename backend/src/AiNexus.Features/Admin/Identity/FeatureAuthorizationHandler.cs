using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;

namespace AiNexus.Features.Admin.Identity;

// 授權每個請求都從 SQL 重讀，讓撤銷立即生效，不必等 cookie 過期。
// 通過時記下授權的功能（稽核與功能使用量依它統計）；稽核端點被功能授權擋下時，寫一筆拒絕的稽核（ADR 0023）。
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http, AuditTrail audit, AppDbContext db)
    : AuthorizationHandler<FeatureRequirement>
{
    private const string DeniedItem = "audit.denied";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var request = http.HttpContext;
        var ct = request?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        if (!user.IsSuccess) { context.Fail(new ErrorFailureReason(this, user.Error)); return; }
        var grants = await access.ForUserAsync(user.Value.Id, ct);
        var granted = requirement.FeatureIds.FirstOrDefault(id => grants.Features.Any(x => x.Id == id));
        if (granted is not null)
        {
            if (request is not null) AuditContextSource.Authorize(request, granted);
            context.Succeed(requirement);
            return;
        }
        context.Fail(new ErrorFailureReason(this, AccessControlErrors.FeatureForbidden));
        // 群組與端點各有功能 policy 時兩個都可能失敗，同一個請求只記一筆。
        if (request?.GetEndpoint().AuditActionOf() is { } action && request.Items.TryAdd(DeniedItem, true))
        {
            AuditContextSource.Authorize(request, requirement.FeatureIds[0]);
            audit.Record(action, user.Value.Id, result: AccessControlErrors.FeatureForbidden.Code, details: AuditTrail.RequestDetails(request));
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
