using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Roles;

public sealed record AdminRoleDto(string Id, string Name, bool Enabled, IReadOnlyList<string> GroupIds, int UserCount);

public sealed record AdminGroupDto(string Id, string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy);

public sealed record AdminFeatureDto(string Id, string Name, string Route, int SortOrder, bool Enabled);

public sealed record AdminCatalogDto(IReadOnlyList<AdminRoleDto> Roles, IReadOnlyList<AdminGroupDto> Groups, IReadOnlyList<AdminFeatureDto> Features, IReadOnlyList<ModelDto> Models);

/// <summary>Every role, group (with its model policy) and feature, and the configured models.</summary>
internal sealed class GetAdminCatalog(NexusDbContext db, ModelCatalog catalog)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/catalog", async (GetAdminCatalog handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetAdminCatalog");

    public async Task<AdminCatalogDto> HandleAsync(CancellationToken ct)
    {
        var roles = await db.Set<Role>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var groups = await db.Set<RoleGroup>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var features = await db.Set<Feature>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var links = await db.Set<RoleGroupRole>().AsNoTracking().ToListAsync(ct);
        var grants = await db.Set<RoleGroupFeature>().AsNoTracking().ToListAsync(ct);
        var policies = await db.Set<GroupModelPolicy>().AsNoTracking().ToDictionaryAsync(x => x.GroupId, ct);
        var counts = await db.Set<UserRole>().GroupBy(x => x.RoleId).Select(x => new { RoleId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);
        return new(roles.Select(x => new AdminRoleDto(x.Id, x.Name, x.Enabled, links.Where(y => y.RoleId == x.Id).Select(y => y.GroupId).ToArray(), counts.GetValueOrDefault(x.Id))).ToArray(),
            groups.Select(x => new AdminGroupDto(x.Id, x.Name, x.Enabled, grants.Where(y => y.GroupId == x.Id).Select(y => y.FeatureId).ToArray(), policies.TryGetValue(x.Id, out var p) ? new(ModelPolicyService.Allowed(p.AllowedModelsJson), ModelPolicyService.Limits(p.DailyTokenLimitsJson), p.StoredAttachmentLimitBytes) : null)).ToArray(),
            features.Select(x => new AdminFeatureDto(x.Id, x.Name, x.Route, x.SortOrder, x.Enabled)).ToArray(), (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray());
    }
}
