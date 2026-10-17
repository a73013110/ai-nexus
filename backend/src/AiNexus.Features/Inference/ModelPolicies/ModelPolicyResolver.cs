namespace AiNexus.Features.Inference.ModelPolicies;

/// <summary>
/// 解析後的有效模型原則，包含允許的模型識別碼清單與各模型每日 Token 上限。
/// </summary>
public sealed record ResolvedModelPolicy(
    IReadOnlyList<string>? AllowedModelIds,
    IReadOnlyDictionary<string, long> DailyTokenLimits);

/// <summary>
/// 模型原則解析器。
/// 處理多重群組原則與個人特定原則的合併邏輯：
/// 1. 允許的模型：群組之間採「聯集累加」，若個人有指定白名單則進行「交集限縮」；
/// 2. 每日 Token 配額：多個群組間採用寬鬆原則（取 Max Cap）；個人覆寫具有最高優先權。
/// </summary>
public static class ModelPolicyResolver
{
    public static ResolvedModelPolicy Resolve(IReadOnlyList<ModelPolicyRequest> groups, ModelPolicyRequest personal)
    {
        // 群組原則：若任一群組未限制（null）則代表全部允許；否則取所有群組允許模型的聯集
        HashSet<string>? allowed = groups.Any(x => x.AllowedModelIds is null)
            ? null
            : groups.SelectMany(x => x.AllowedModelIds!).ToHashSet(StringComparer.Ordinal);

        // 個人白名單：若有設定則進一步與群組許可進行交集限縮
        if (personal.AllowedModelIds is { } personalModels)
        {
            if (allowed is null)
            {
                allowed = personalModels.ToHashSet(StringComparer.Ordinal);
            }
            else
            {
                allowed.IntersectWith(personalModels);
            }
        }

        var limits = new Dictionary<string, long>(StringComparer.Ordinal);
        var distinctModels = groups
            .SelectMany(x => x.DailyTokenLimits?.Keys ?? [])
            .Distinct(StringComparer.Ordinal);

        foreach (var model in distinctModels)
        {
            // 找出所有允許存取該模型的群組
            var granting = groups
                .Where(x => x.AllowedModelIds is null || x.AllowedModelIds.Contains(model, StringComparer.Ordinal))
                .ToArray();

            // 若所有允許群組皆設定了該模型的上限，取最寬鬆者（Max）；任一群組未設上限即視為無上限
            if (granting.Length > 0 && granting.All(x => x.DailyTokenLimits?.ContainsKey(model) == true))
            {
                limits[model] = granting.Max(x => x.DailyTokenLimits![model]);
            }
        }

        // 個人配額覆寫擁有最高優先權
        if (personal.DailyTokenLimits is { } overrides)
        {
            foreach (var limit in overrides)
            {
                limits[limit.Key] = limit.Value;
            }
        }

        return new(allowed?.Order(StringComparer.Ordinal).ToArray(), limits);
    }
}
