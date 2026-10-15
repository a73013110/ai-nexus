using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Audit;

public sealed record AuditCatalogDto(IReadOnlyList<FeatureDto> Features, IReadOnlyList<ModelDto> Models, IReadOnlyList<NavigationGroupDto> NavigationGroups);

/// <summary>
/// Feature, model and menu-group names for presenting audit entries; every group is listed, since a recorded feature change
/// is arranged as the menu does. Read-only; does not depend on account or policy administration.
/// </summary>
internal sealed class GetAuditCatalog(NexusDbContext db, ModelCatalog catalog, AccessService access)
{
    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("/catalog", async (GetAuditCatalog handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetAuditCatalog");

    public async Task<AuditCatalogDto> HandleAsync(CancellationToken ct) => new(
        await db.Set<Feature>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new FeatureDto(x.Id, x.Name, x.Route, x.GroupId, x.Icon, x.SortOrder)).ToArrayAsync(ct),
        (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray(),
        await access.NavigationGroupsAsync(ct));
}
