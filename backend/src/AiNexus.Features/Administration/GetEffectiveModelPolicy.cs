using AiNexus.Features.Identity;
using AiNexus.Features.Inference;

namespace AiNexus.Features.Administration;

/// <summary>The signed-in user's effective model whitelist, token budgets and storage cap, with public model ids.</summary>
internal static class GetEffectiveModelPolicy
{
    public static void Map(RouteGroupBuilder root) => root
        .MapGet("/settings/model-policy", async (ICurrentUser user, ModelPolicyService service, CancellationToken ct) => Results.Ok(await service.ForAsync(user.Id, ct)))
        .WithName("GetEffectiveModelPolicy").Produces<EffectiveModelPolicyDto>();
}
