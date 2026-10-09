using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Administration.Roles;

/// <summary>Not a request validator: format failures are audited inside the administrative transaction (see <see cref="AccessRules"/>).</summary>
public sealed record FeatureUpdateRequest(string Name, int SortOrder, bool Enabled);

/// <summary>Renames, reorders, enables or disables an existing feature. Audited; refused when it would remove the actor's own administrator access.</summary>
internal sealed class SaveFeature(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/features/{id}", async (string id, FeatureUpdateRequest body, SaveFeature handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SaveFeature");

    public Task<Result> HandleAsync(string id, FeatureUpdateRequest request, CancellationToken ct) => audit.TryMutateAsync("admin.feature", null, id, async () =>
    {
        if ((AccessRules.Key(id) ?? AccessRules.Name(request.Name)) is { } invalid) return invalid;
        if (request.SortOrder is < 0 or > 10000) return AdministrationErrors.InvalidOrder;
        var feature = await db.Set<Feature>().FindAsync([id], ct);
        if (feature is null) return AdministrationErrors.NotFound;
        feature.Name = request.Name.Trim(); feature.SortOrder = request.SortOrder; feature.Enabled = request.Enabled;
        return Result.Success;
    }, ct);
}
