using AiNexus.Features.Identity;

namespace AiNexus.Features.Inference.ModelPolicies;

/// <summary>目前登入者的有效模型白名單、Token 額度與儲存上限，回傳對外的模型 id。</summary>
internal sealed class GetEffectiveModelPolicy(ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder root) => root
        .MapGet("/settings/model-policy", async (ICurrentUser user, GetEffectiveModelPolicy handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("GetEffectiveModelPolicy");

    public Task<EffectiveModelPolicyDto> HandleAsync(Guid owner, CancellationToken ct) => policies.ForAsync(owner, ct);
}
