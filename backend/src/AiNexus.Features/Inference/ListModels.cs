using AiNexus.Features.Identity;

namespace AiNexus.Features.Inference;

/// <summary>The models the user's groups allow, with a default the user may actually use.</summary>
internal static class ListModels
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/models", async (ModelCatalog models, ICurrentUser user, ModelPolicyService policies, CancellationToken ct) =>
            Results.Ok(await HandleAsync(models, policies, user.Id, ct)))
        .WithName("ListModels").Produces<ModelsDto>();

    public static async Task<ModelsDto> HandleAsync(ModelCatalog models, ModelPolicyService policies, Guid owner, CancellationToken ct)
    {
        var catalog = await models.GetAsync(ct);
        var policy = await policies.ForAsync(owner, ct);
        var allowed = catalog.Models.Where(x => policy.AllowedModelIds is null || policy.AllowedModelIds.Contains(x.Id)).ToArray();
        var defaultId = allowed.Any(x => x.Id == catalog.Policy.DefaultModelId) ? catalog.Policy.DefaultModelId : allowed.FirstOrDefault()?.Id;
        return catalog with { Models = allowed, Notice = catalog.Models.Count > 0 && allowed.Length == 0 ? "你的群組目前沒有可用模型，請由管理員確認群組允許的模型與目前服務設定。" : catalog.Notice,
            Policy = catalog.Policy with { DefaultModelId = defaultId } };
    }
}
