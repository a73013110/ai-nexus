using AiNexus.Features.Identity;

namespace AiNexus.Features.Inference;

/// <summary>The models the user's groups allow, with a default the user may actually use.</summary>
internal sealed class ListModels(ModelCatalog models, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/models", async (ICurrentUser user, ListModels handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListModels");

    public async Task<ModelsDto> HandleAsync(Guid owner, CancellationToken ct)
    {
        var catalog = await models.GetAsync(ct);
        var policy = await policies.ForAsync(owner, ct);
        var allowed = catalog.Models.Where(x => policy.AllowedModelIds is null || policy.AllowedModelIds.Contains(x.Id)).ToArray();
        var defaultId = allowed.Any(x => x.Id == catalog.Policy.DefaultModelId) ? catalog.Policy.DefaultModelId : allowed.FirstOrDefault()?.Id;
        return catalog with { Models = allowed, Notice = catalog.Models.Count > 0 && allowed.Length == 0 ? "你的群組目前沒有可用模型，請由管理員確認群組允許的模型與目前服務設定。" : catalog.Notice,
            Policy = catalog.Policy with { DefaultModelId = defaultId } };
    }
}
