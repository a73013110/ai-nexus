using AiNexus.Features.Identity;
using AiNexus.Features.Inference.ModelPolicies;

namespace AiNexus.Features.Inference.Models;

/// <summary>
/// 模型列表端點處理常式。
/// 根據當前使用者所屬群組的模型原則，過濾出使用者有權存取的模型清單，
/// 並決定使用者可用的預設模型。
/// </summary>
internal sealed class ListModels(ModelCatalog models, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/models", async (ICurrentUser user, ListModels handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListModels");

    public async Task<ModelsDto> HandleAsync(Guid owner, CancellationToken ct)
    {
        var catalog = await models.GetAsync(ct);
        var policy = await policies.ForAsync(owner, ct);

        // 依群組原則過濾允許使用的模型；若 Policy.AllowedModelIds 為 null 則代表不限制
        var allowed = catalog.Models
            .Where(x => policy.AllowedModelIds is null || policy.AllowedModelIds.Contains(x.Id))
            .ToArray();

        // 決定預設模型：若全域預設在許可清單內則使用，否則降級選用第一個許可模型
        var defaultId = allowed.Any(x => x.Id == catalog.Policy.DefaultModelId)
            ? catalog.Policy.DefaultModelId
            : allowed.FirstOrDefault()?.Id;

        // 若系統有模型但該使用者所屬群組無任何模型授權，提示使用者聯絡管理員
        var notice = catalog.Models.Count > 0 && allowed.Length == 0
            ? "你的群組目前沒有可用模型，請由管理員確認群組允許的模型與目前服務設定。"
            : catalog.Notice;

        return catalog with
        {
            Models = allowed,
            Notice = notice,
            Policy = catalog.Policy with { DefaultModelId = defaultId }
        };
    }
}
