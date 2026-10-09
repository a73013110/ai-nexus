using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Audit;

public sealed record AuditCatalogDto(IReadOnlyList<FeatureDto> Features, IReadOnlyList<ModelDto> Models);

/// <summary>Feature and model names for the audit filters. Read-only; does not depend on account or policy administration.</summary>
internal static class GetAuditCatalog
{
    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("/catalog", async (NexusDbContext db, ModelCatalog catalog, CancellationToken ct) => Results.Ok(new AuditCatalogDto(
            await db.Set<Feature>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new FeatureDto(x.Id, x.Name, x.Route)).ToArrayAsync(ct),
            (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray())))
        .WithName("GetAuditCatalog").Produces<AuditCatalogDto>();
}
