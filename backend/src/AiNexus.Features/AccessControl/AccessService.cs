using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// Effective grants read from SQL. Within one HTTP request each user's grants are read at most once; the next request
/// reads them again, so revocation still applies to every later request. Background jobs and the fresh scopes that
/// long-lived requests create for periodic checks are not the request's own scope and always re-read.
/// </summary>
public sealed class AccessService(NexusDbContext db, IActiveUsers activeUsers, IHttpContextAccessor http, IServiceProvider scope)
{
    private readonly Dictionary<Guid, AccessDto> grants = [];

    /// <summary>Forgets grants read so far in this request; call after a write that may change them, or before a re-check that must see SQL.</summary>
    public void Invalidate() => grants.Clear();

    public IQueryable<UserGroupGrant> GroupMemberships(IReadOnlyList<Guid> users) =>
        (from userId in activeUsers.Ids join assignment in db.Set<UserRole>() on userId equals assignment.UserId
         join role in db.Set<Role>() on assignment.RoleId equals role.Id
         join link in db.Set<RoleGroupRole>() on role.Id equals link.RoleId
         join roleGroup in db.Set<RoleGroup>() on link.GroupId equals roleGroup.Id
         where users.Contains(userId) && role.Enabled && roleGroup.Enabled
         select new UserGroupGrant { UserId = userId, GroupId = roleGroup.Id }).Distinct();
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
        if (!await activeUsers.Ids.AnyAsync(x => x == user, ct)) return new([], [], []);
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

/// <summary>Ids of enabled, not deleted users. Identity implements it, so grants can be filtered without reading its user table here.</summary>
public interface IActiveUsers
{
    IQueryable<Guid> Ids { get; }
}

public sealed class UserGroupGrant
{
    public Guid UserId { get; init; }
    public string GroupId { get; init; } = "";
}

/// <summary>Evaluated by Identity's feature authorization handler against the signed-in user's current grants.</summary>
public sealed record FeatureRequirement(params string[] FeatureIds) : IAuthorizationRequirement;
