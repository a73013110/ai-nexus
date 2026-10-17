using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;

namespace AiNexus.Features.Administration.Navigation;

[ValidatedInHandler("Format failures are audited inside the administrative transaction (NavigationRules).")]
public sealed record NavigationGroupCreateRequest(string Id, string Name, string? ParentId, int SortOrder);

/// <summary>新增選單群組，可放在頂層或某群組之下。會稽核。</summary>
internal sealed class CreateNavigationGroup(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/navigation-groups", async (NavigationGroupCreateRequest body, CreateNavigationGroup handler, CancellationToken ct) => (await handler.HandleAsync(body, ct)).ToHttpResult())
        .WithName("CreateNavigationGroup");

    public Task<Result> HandleAsync(NavigationGroupCreateRequest request, CancellationToken ct) => audit.MutateAsync("admin.navigation_group", null, request.Id, async () =>
    {
        if ((AccessRules.Key(request.Id) ?? AccessRules.Name(request.Name) ?? NavigationRules.Order(request.SortOrder)) is { } invalid) return invalid;
        if (await db.Set<NavigationGroup>().FindAsync([request.Id], ct) is not null) return AdministrationErrors.NavigationGroupExists;
        if (await NavigationRules.ExistingGroupAsync(db, request.ParentId, ct) is { } missing) return missing;
        db.Add(new NavigationGroup { Id = request.Id, Name = request.Name.Trim(), ParentId = request.ParentId, SortOrder = request.SortOrder });
        return Result.Success;
    }, ct);
}
