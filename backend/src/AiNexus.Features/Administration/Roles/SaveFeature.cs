using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration.Navigation;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;

namespace AiNexus.Features.Administration.Roles;

/// <summary><c>GroupId</c> null places the feature at the menu's top level; <c>SortOrder</c> orders it within that group.</summary>
[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record FeatureUpdateRequest(string Name, int SortOrder, bool Enabled, string? GroupId, string Icon);

/// <summary>
/// Renames, reorders, regroups, re-icons, enables or disables an existing feature. Audited; refused when it would remove
/// the actor's own administrator access.
/// </summary>
internal sealed class SaveFeature(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/features/{id}", async (string id, FeatureUpdateRequest body, SaveFeature handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SaveFeature");

    public Task<Result> HandleAsync(string id, FeatureUpdateRequest request, CancellationToken ct) => audit.MutateAsync("admin.feature", null, id, async () =>
    {
        // No id format rule: modules choose feature ids (`logs.query`), and an unknown id is simply not found.
        if ((AccessRules.Name(request.Name) ?? NavigationRules.Order(request.SortOrder) ?? NavigationRules.Icon(request.Icon)) is { } invalid) return invalid;
        var feature = await db.Set<Feature>().FindAsync([id], ct);
        if (feature is null) return AdministrationErrors.NotFound;
        if (await NavigationRules.ExistingGroupAsync(db, request.GroupId, ct) is { } missing) return missing;
        feature.Name = request.Name.Trim(); feature.SortOrder = request.SortOrder; feature.Enabled = request.Enabled;
        feature.GroupId = request.GroupId; feature.Icon = request.Icon;
        return Result.Success;
    }, ct);
}
