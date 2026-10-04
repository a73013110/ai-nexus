using AiNexus.BuildingBlocks;
using AiNexus.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.AccessControl;

public sealed class AccessService(NexusDbContext db)
{
    public async Task<AccessDto> ForUserAsync(Guid user, CancellationToken ct)
    {
        var roleIds = db.Set<UserRole>().Where(x => x.UserId == user).Select(x => x.RoleId);
        var roles = await db.Set<Role>().AsNoTracking().Where(x => roleIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var enabledRoles = roles.Select(x => x.Id).ToArray();
        var groupIds = db.Set<RoleGroupRole>().Where(x => enabledRoles.Contains(x.RoleId)).Select(x => x.GroupId);
        var groups = await db.Set<RoleGroup>().AsNoTracking().Where(x => groupIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var enabledGroups = groups.Select(x => x.Id).ToArray();
        var featureIds = db.Set<RoleGroupFeature>().Where(x => enabledGroups.Contains(x.GroupId)).Select(x => x.FeatureId);
        var features = await db.Set<Feature>().AsNoTracking().Where(x => featureIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new FeatureDto(x.Id, x.Name, x.Route)).ToListAsync(ct);
        return new(roles, groups, features);
    }
}

public sealed record FeatureRequirement(params string[] FeatureIds) : IAuthorizationRequirement;

// Grants are read from SQL per request so revocation does not wait for a cookie to expire.
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var ct = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        var grants = await access.ForUserAsync(user.Id, ct);
        if (grants.Features.Any(x => requirement.FeatureIds.Contains(x.Id))) context.Succeed(requirement);
        else throw new ApiException(403, "feature_forbidden", "你的角色目前沒有使用此功能的權限，請聯絡管理員。");
    }
}
