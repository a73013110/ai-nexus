using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Administration;

public sealed record ModelPolicyRequest(IReadOnlyList<string>? AllowedModelIds = null, IReadOnlyDictionary<string, long>? DailyTokenLimits = null);
public sealed record ModelTokenBudgetDto(string ModelId, long? DailyTokenLimit, long UsedTokens, long ReservedTokens, long? RemainingTokens, string Source);
public sealed record EffectiveModelPolicyDto(IReadOnlyList<string>? AllowedModelIds, long? StoredAttachmentLimitBytes, IReadOnlyList<ModelTokenBudgetDto> Models, DateTimeOffset ResetsAt);
public sealed record AdminUserModelPolicyDto(ModelPolicyRequest Personal, EffectiveModelPolicyDto Effective);

// The same policy and accounting apply to chat, OCR, transformations and evaluations.
public sealed class ModelPolicyService(NexusDbContext db, AccessService access, ModelPresentation presentation, IOptions<InferenceOptions> inference)
{
    public const long MaximumTokenLimit = 1_000_000_000_000;
    public static string[]? Allowed(string? json) => json is null ? null : JsonSerializer.Deserialize<string[]>(json);
    public static Dictionary<string, long> Limits(string? json) => json is null ? [] : JsonSerializer.Deserialize<Dictionary<string, long>>(json)!;
    public static string? SerializeAllowed(IReadOnlyList<string>? ids) => ids is null ? null : JsonSerializer.Serialize(ids.Order(StringComparer.Ordinal));
    public static string? SerializeLimits(IReadOnlyDictionary<string, long>? limits) => limits is null || limits.Count == 0 ? null : JsonSerializer.Serialize(limits.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary());
    public void Validate(ModelPolicyRequest policy)
    {
        var ids = inference.Value.Models.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (policy.AllowedModelIds is { } allowed && (allowed.Count > 24 || allowed.Distinct(StringComparer.Ordinal).Count() != allowed.Count || allowed.Any(x => !ids.Contains(x))) ||
            policy.DailyTokenLimits is { } limits && (limits.Count > 24 || limits.Any(x => !ids.Contains(x.Key) || x.Value is < 0 or > MaximumTokenLimit)))
            throw new ApiException(400, "invalid_model_policy", "模型清單或 token 上限不正確。上限需為 0 至 1,000,000,000,000 的整數。");
    }
    public async Task<AdminUserModelPolicyDto> AdministrativeAsync(Guid owner, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == owner && x.DeletedAt == null, ct)) throw new ApiException(404, "admin_resource_not_found", "找不到此使用者。");
        var personal = await db.Set<UserModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner, ct);
        return new(new(Allowed(personal?.AllowedModelsJson), Limits(personal?.DailyTokenLimitsJson)), await ForAsync(owner, ct, publicIds: false));
    }
    public async Task<EffectiveModelPolicyDto> ForAsync(Guid owner, CancellationToken ct, bool publicIds = true, DateTimeOffset? asOf = null)
    {
        var grants = await access.ForUserAsync(owner, ct); var groups = grants.Groups.Select(x => x.Id).ToArray();
        var policies = await db.Set<GroupModelPolicy>().AsNoTracking().Where(x => groups.Contains(x.GroupId)).ToListAsync(ct);
        var personal = await db.Set<UserModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner, ct);
        HashSet<string>? allowed = null;
        foreach (var list in policies.Select(x => Allowed(x.AllowedModelsJson)).Append(Allowed(personal?.AllowedModelsJson)).Where(x => x is not null))
        {
            if (allowed is null) allowed = list!.ToHashSet(StringComparer.Ordinal); else allowed.IntersectWith(list!);
        }
        var groupLimits = policies.SelectMany(x => Limits(x.DailyTokenLimitsJson)).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Min(x => x.Value));
        var overrides = Limits(personal?.DailyTokenLimitsJson);
        var start = UtcDay.Start(asOf ?? DateTimeOffset.UtcNow); var end = start.AddDays(1);
        var usage = await db.Runs.AsNoTracking().Where(x => x.OwnerId == owner && x.CreatedAt >= start && x.CreatedAt < end)
            .Select(x => new { x.ModelId, x.InputTokens, x.OutputTokens, x.ReservedTokens, Active = x.ActiveOwnerId != null })
            .Concat(db.Set<ModelInvocation>().AsNoTracking().Where(x => x.OwnerId == owner && x.CreatedAt >= start && x.CreatedAt < end && x.Kind != "embedding" && x.Kind != "web-search")
                .Select(x => new { x.ModelId, x.InputTokens, x.OutputTokens, x.ReservedTokens, Active = x.Status == "running" }))
            .GroupBy(x => x.ModelId).Select(g => new
            {
                ModelId = g.Key,
                Used = g.Sum(x => !x.Active && x.InputTokens != null && x.OutputTokens != null ? x.InputTokens.Value + x.OutputTokens.Value : 0),
                Reserved = g.Sum(x => x.Active || x.InputTokens == null || x.OutputTokens == null ? x.ReservedTokens : 0)
            }).ToDictionaryAsync(x => x.ModelId, ct);
        string Id(string id) => publicIds ? presentation.PublicId(id) : id;
        var budgets = inference.Value.Models.Select(model =>
        {
            long? limit = overrides.TryGetValue(model.Id, out var personalLimit) ? personalLimit : groupLimits.TryGetValue(model.Id, out var groupLimit) ? groupLimit : null;
            var value = usage.GetValueOrDefault(model.Id);
            return new ModelTokenBudgetDto(Id(model.Id), limit, value?.Used ?? 0, value?.Reserved ?? 0,
                limit is long cap ? Math.Max(0, cap - (value?.Used ?? 0) - (value?.Reserved ?? 0)) : null,
                overrides.ContainsKey(model.Id) ? "personal" : groupLimits.ContainsKey(model.Id) ? "group" : "unlimited");
        }).ToArray();
        return new(allowed?.Select(Id).ToArray(), policies.Select(x => x.StoredAttachmentLimitBytes).Min(), budgets, end);
    }
    public async Task RequireAsync(Guid owner, string internalModel, CancellationToken ct, bool checkQuota = true)
    {
        var value = await ForAsync(owner, ct, publicIds: false);
        if (value.AllowedModelIds is not null && !value.AllowedModelIds.Contains(internalModel))
            throw new ApiException(403, "model_group_forbidden", "此模型不在你的個人或群組授權範圍。");
        if (checkQuota && value.Models.FirstOrDefault(x => x.ModelId == internalModel)?.RemainingTokens is <= 0) throw Exhausted();
    }
    // Call inside the owner row-lock transaction, then persist the reservation before releasing it.
    public async Task<GenerationParameters> BudgetAsync(Guid owner, string model, GenerationParameters parameters, long inputEstimate, DateTimeOffset createdAt, CancellationToken ct)
    {
        var value = await ForAsync(owner, ct, publicIds: false, asOf: createdAt);
        if (value.AllowedModelIds is not null && !value.AllowedModelIds.Contains(model)) throw new ApiException(403, "model_group_forbidden", "此模型不在你的個人或群組授權範圍。");
        var remaining = value.Models.First(x => x.ModelId == model).RemainingTokens;
        var output = remaining is long cap ? Math.Min(parameters.MaxOutputTokens, cap - inputEstimate) : parameters.MaxOutputTokens;
        if (output < 1) throw Exhausted();
        // Preserve the original history trimming budget when reducing the output allowance.
        return parameters with { MaxOutputTokens = (int)output, ContextTokens = parameters.ContextTokens - parameters.MaxOutputTokens + (int)output };
    }
    private static ApiException Exhausted() => new(429, "model_token_quota", "此模型今日可用 token 不足（包含輸入、輸出與待結算預留）。請縮短提問、切換模型或聯絡管理員；台北時間每日 08:00 重設。");
}
