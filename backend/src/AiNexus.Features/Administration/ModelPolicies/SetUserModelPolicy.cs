using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.ModelPolicies;

/// <summary>
/// Saves a user's personal whitelist and token caps; an empty policy removes the row. Audited. The policy rules are
/// checked inside the administrative transaction (not by a request validator) so a rejected change is audited too.
/// </summary>
internal sealed class SetUserModelPolicy(NexusDbContext db, AdministrativeAudit audit, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/model-policy", async (Guid id, ModelPolicyRequest body, SetUserModelPolicy handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SetUserModelPolicy");

    public Task<Result> HandleAsync(Guid id, ModelPolicyRequest request, CancellationToken ct) => audit.MutateAsync("admin.user_model_policy", id, id.ToString(), async () =>
    {
        if (policies.Check(request) is { } invalid) return invalid;
        // Takes the user's row lock and confirms the user is not deleted.
        if (await db.Users.Where(x => x.Id == id && x.DeletedAt == null).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct) != 1) return AdministrationErrors.NotFound;
        var value = await db.Set<UserModelPolicy>().FindAsync([id], ct);
        if (value is null) { value = new() { UserId = id }; db.Add(value); }
        value.AllowedModelsJson = ModelPolicyService.SerializeAllowed(request.AllowedModelIds);
        value.DailyTokenLimitsJson = ModelPolicyService.SerializeLimits(request.DailyTokenLimits);
        if (value.AllowedModelsJson is null && value.DailyTokenLimitsJson is null) db.Remove(value);
        return Result.Success;
    }, ct);
}
