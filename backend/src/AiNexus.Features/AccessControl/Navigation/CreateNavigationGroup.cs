using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.AccessControl.Management;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;

namespace AiNexus.Features.AccessControl.Navigation;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (NavigationRules).")]
public sealed record NavigationGroupCreateRequest(string Id, string Name, string? ParentId, int SortOrder);

/// <summary>新增選單群組，可放在頂層或某群組之下。會稽核。</summary>
internal sealed class CreateNavigationGroup(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/navigation-groups", async (NavigationGroupCreateRequest body, IRequestActor actor, CreateNavigationGroup handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, body, ct)).ToHttpResult())
        .WithName("CreateNavigationGroup");

    public Task<Result> HandleAsync(Guid actor, NavigationGroupCreateRequest request, CancellationToken ct) => audit.MutateAsync(actor, "admin.navigation_group", null, request.Id, token => NavigationRules.SnapshotAsync(db, request.Id, token), async () =>
    {
        if ((AccessRules.Key(request.Id) ?? AccessRules.Name(request.Name) ?? NavigationRules.Order(request.SortOrder)) is { } invalid) return invalid;
        if (await db.Set<NavigationGroup>().FindAsync([request.Id], ct) is not null) return AccessControlErrors.NavigationGroupExists;
        if (await NavigationRules.ExistingGroupAsync(db, request.ParentId, ct) is { } missing) return missing;
        db.Add(new NavigationGroup { Id = request.Id, Name = request.Name.Trim(), ParentId = request.ParentId, SortOrder = request.SortOrder });
        return Result.Success;
    }, ct);
}
