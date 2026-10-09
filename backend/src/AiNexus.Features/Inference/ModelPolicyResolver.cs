namespace AiNexus.Features.Inference;

public sealed record ResolvedModelPolicy(IReadOnlyList<string>? AllowedModelIds, IReadOnlyDictionary<string, long> DailyTokenLimits);

// Groups grant access additively; only a personal whitelist narrows those grants.
public static class ModelPolicyResolver
{
    public static ResolvedModelPolicy Resolve(IReadOnlyList<ModelPolicyRequest> groups, ModelPolicyRequest personal)
    {
        HashSet<string>? allowed = groups.Any(x => x.AllowedModelIds is null)
            ? null : groups.SelectMany(x => x.AllowedModelIds!).ToHashSet(StringComparer.Ordinal);
        if (personal.AllowedModelIds is { } personalModels)
        {
            if (allowed is null) allowed = personalModels.ToHashSet(StringComparer.Ordinal);
            else allowed.IntersectWith(personalModels);
        }

        var limits = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var model in groups.SelectMany(x => x.DailyTokenLimits?.Keys ?? []).Distinct(StringComparer.Ordinal))
        {
            var granting = groups.Where(x => x.AllowedModelIds is null || x.AllowedModelIds.Contains(model, StringComparer.Ordinal)).ToArray();
            // An omitted cap is unlimited, including a group with no saved policy.
            if (granting.Length > 0 && granting.All(x => x.DailyTokenLimits?.ContainsKey(model) == true))
                limits[model] = granting.Max(x => x.DailyTokenLimits![model]);
        }
        if (personal.DailyTokenLimits is { } overrides)
            foreach (var limit in overrides) limits[limit.Key] = limit.Value;
        return new(allowed?.Order(StringComparer.Ordinal).ToArray(), limits);
    }
}
