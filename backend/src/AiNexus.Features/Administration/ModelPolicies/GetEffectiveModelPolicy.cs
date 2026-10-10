using AiNexus.Features.Identity;
using AiNexus.Features.Inference;

namespace AiNexus.Features.Administration.ModelPolicies;

/// <summary>The signed-in user's effective model whitelist, token budgets and storage cap, with public model ids.</summary>
internal sealed class GetEffectiveModelPolicy(ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder root) => root
        .MapGet("/settings/model-policy", async (ICurrentUser user, GetEffectiveModelPolicy handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("GetEffectiveModelPolicy");

    public Task<EffectiveModelPolicyDto> HandleAsync(Guid owner, CancellationToken ct) => policies.ForAsync(owner, ct);
}
