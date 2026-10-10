using AiNexus.Features.AccessControl;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using AiNexus.Features.Identity.Sessions;

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
        {
            context.Fail(new ErrorFailureReason(this, IdentityErrors.TestAdminReadOnly));
            return;
        }
        var ct = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        if (!user.IsSuccess) { context.Fail(new ErrorFailureReason(this, user.Error)); return; }
        var grants = await access.ForUserAsync(user.Value.Id, ct);
        if (grants.Features.Any(x => requirement.FeatureIds.Contains(x.Id))) context.Succeed(requirement);
        else context.Fail(new ErrorFailureReason(this, IdentityErrors.FeatureForbidden));
    }
}
