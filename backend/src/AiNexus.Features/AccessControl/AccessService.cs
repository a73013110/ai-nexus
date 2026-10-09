using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// Effective grants read from SQL. Within one HTTP request each user's grants are read at most once; the next request
/// reads them again, so revocation still applies to every later request. Background jobs and the fresh scopes that
/// long-lived requests create for periodic checks are not the request's own scope and always re-read.
/// </summary>
public sealed class AccessService(NexusDbContext db, IHttpContextAccessor http, IServiceProvider scope)
{
    private readonly Dictionary<Guid, AccessDto> grants = [];

    /// <summary>Forgets grants read so far in this request; call after a write that may change them, or before a re-check that must see SQL.</summary>
    public void Invalidate() => grants.Clear();


    public IQueryable<UserGroupGrant> GroupMemberships(IReadOnlyList<Guid> users) =>
        (from user in db.Users join assignment in db.Set<UserRole>() on user.Id equals assignment.UserId
         join role in db.Set<Role>() on assignment.RoleId equals role.Id
         join link in db.Set<RoleGroupRole>() on role.Id equals link.RoleId
         join roleGroup in db.Set<RoleGroup>() on link.GroupId equals roleGroup.Id
         where users.Contains(user.Id) && user.Enabled && user.DeletedAt == null && role.Enabled && roleGroup.Enabled
         select new UserGroupGrant { UserId = user.Id, GroupId = roleGroup.Id }).Distinct();
    public async Task<AccessDto> ForUserAsync(Guid user, CancellationToken ct)
    {
        var memoize = ReferenceEquals(http.HttpContext?.RequestServices, scope);
        if (memoize && grants.TryGetValue(user, out var cached)) return cached;
        var value = await ReadAsync(user, ct);
        if (memoize) grants[user] = value;
        return value;
    }

    private async Task<AccessDto> ReadAsync(Guid user, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == user && x.Enabled && x.DeletedAt == null, ct)) return new([], [], []);
        var roleIds = db.Set<UserRole>().Where(x => x.UserId == user).Select(x => x.RoleId);
        var roles = await db.Set<Role>().AsNoTracking().Where(x => roleIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var groupIds = GroupMemberships([user]).Select(x => x.GroupId);
        var groups = await db.Set<RoleGroup>().AsNoTracking().Where(x => groupIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var enabledGroups = groups.Select(x => x.Id).ToArray();
        var featureIds = db.Set<RoleGroupFeature>().Where(x => enabledGroups.Contains(x.GroupId)).Select(x => x.FeatureId);
        var features = await db.Set<Feature>().AsNoTracking().Where(x => featureIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new FeatureDto(x.Id, x.Name, x.Route)).ToListAsync(ct);
        return new(roles, groups, features);
    }
}

public sealed class UserGroupGrant
{
    public Guid UserId { get; init; }
    public string GroupId { get; init; } = "";
}

public sealed record FeatureRequirement(params string[] FeatureIds) : IAuthorizationRequirement;

// Grants are read from SQL once per request so revocation does not wait for a cookie to expire.
public sealed class FeatureAuthorizationHandler(CurrentUser current, AccessService access, IHttpContextAccessor http)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        if (requirement.FeatureIds.Contains(AdministrationConfiguration.Feature) && context.User.HasClaim(x => x.Type == SessionIdentity.ActorId) &&
            http.HttpContext?.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            throw new ApiException(403, "test_admin_read_only", "測試身分期間只能檢視管理功能；請先返回管理者再異動帳號與授權。");
        var ct = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await current.GetAsync(ct);
        var grants = await access.ForUserAsync(user.Id, ct);
        if (grants.Features.Any(x => requirement.FeatureIds.Contains(x.Id))) context.Succeed(requirement);
        else throw new ApiException(403, "feature_forbidden", "你的角色目前沒有使用此功能的權限，請聯絡管理員。");
    }
}
