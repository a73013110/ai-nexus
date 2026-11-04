using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Inference.ModelPolicies;

/// <summary>目前登入者的有效模型白名單、Token 額度與儲存上限，回傳對外的模型 id。</summary>
internal sealed class GetEffectiveModelPolicy(ModelPolicyService policies) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder root) => root
        .MapGet("/settings/model-policy", (ICurrentUser user, GetEffectiveModelPolicy handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .NotAudited("使用者自己的模型政策")
        .WithName("GetEffectiveModelPolicy");

    public Task<EffectiveModelPolicyDto> HandleAsync(Guid owner, CancellationToken ct) => policies.ForAsync(owner, ct);
}
