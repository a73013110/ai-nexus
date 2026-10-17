using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.ModelPolicies;
using AiNexus.Features.Inference.ModelTasks;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Time;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference.ModelPolicies;

/// <summary>
/// 模型原則設定請求資料結構。
/// </summary>
[ValidatedInHandler("The rules are checked inside the audited administrative transaction (ModelPolicyService.Check).")]
public sealed record ModelPolicyRequest(
    IReadOnlyList<string>? AllowedModelIds = null,
    IReadOnlyDictionary<string, long>? DailyTokenLimits = null);

/// <summary>
/// 特定模型的 Token 預算與使用量統計資料傳輸物件。
/// </summary>
public sealed record ModelTokenBudgetDto(
    string ModelId,
    long? DailyTokenLimit,
    long UsedTokens,
    long ReservedTokens,
    long? RemainingTokens,
    string Source,
    string? ModelDisplayName = null);

/// <summary>
/// 使用者當前生效之模型使用原則，包含許可模型、附件限制與各模型配額。
/// </summary>
public sealed record EffectiveModelPolicyDto(
    IReadOnlyList<string>? AllowedModelIds,
    long? StoredAttachmentLimitBytes,
    IReadOnlyList<ModelTokenBudgetDto> Models,
    DateTimeOffset ResetsAt);

/// <summary>
/// 模型使用原則與每日 Token 配額管理服務。
/// 統一負責對話（Chat）、文件 OCR、文字轉換與評估等所有推論任務的權限校驗與配額扣減。
/// </summary>
public sealed class ModelPolicyService(
    NexusDbContext db,
    AccessService access,
    ModelPresentation presentation,
    IOptions<InferenceOptions> inference,
    TimeProvider clock)
{
    /// <summary>單一模型每日最大 Token 上限防禦門檻（1 兆）。</summary>
    public const long MaximumTokenLimit = 1_000_000_000_000;

    public static string[]? Allowed(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<string[]>(json);

    public static Dictionary<string, long> Limits(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<Dictionary<string, long>>(json)!;

    public static string? SerializeAllowed(IReadOnlyList<string>? ids) =>
        ids is null ? null : JsonSerializer.Serialize(ids.Order(StringComparer.Ordinal));

    public static string? SerializeLimits(IReadOnlyDictionary<string, long>? limits) =>
        limits is null || limits.Count == 0
            ? null
            : JsonSerializer.Serialize(limits.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary());

    /// <summary>
    /// 驗證模型原則設定的合法性（僅限已設定模型、上限 24 個設定、配額介於 0 與上限之間）。
    /// </summary>
    internal Error? Check(ModelPolicyRequest policy)
    {
        var ids = inference.Value.Models.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

        var allowedInvalid = policy.AllowedModelIds is { } allowed && (
            allowed.Count > 24 ||
            allowed.Distinct(StringComparer.Ordinal).Count() != allowed.Count ||
            allowed.Any(x => !ids.Contains(x)));

        var limitsInvalid = policy.DailyTokenLimits is { } limits && (
            limits.Count > 24 ||
            limits.Any(x => !ids.Contains(x.Key) || x.Value is < 0 or > MaximumTokenLimit));

        if (allowedInvalid || limitsInvalid)
        {
            return InferenceErrors.InvalidModelPolicy;
        }

        return null;
    }

    /// <summary>
    /// 計算指定使用者在特定時間點的有效模型原則與 Token 預算狀況。
    /// </summary>
    public async Task<EffectiveModelPolicyDto> ForAsync(
        Guid owner,
        CancellationToken ct,
        bool publicIds = true,
        DateTimeOffset? asOf = null)
    {
        var grants = await access.ForUserAsync(owner, ct);
        var groups = grants.Groups.Select(x => x.Id).ToArray();

        var policies = await db.Set<GroupModelPolicy>()
            .AsNoTracking()
            .Where(x => groups.Contains(x.GroupId))
            .ToListAsync(ct);

        var personal = await db.Set<UserModelPolicy>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == owner, ct);

        var byGroup = policies.ToDictionary(x => x.GroupId, StringComparer.Ordinal);
        var overrides = Limits(personal?.DailyTokenLimitsJson);

        var effective = ModelPolicyResolver.Resolve(
            groups.Select(id => byGroup.TryGetValue(id, out var policy)
                ? new ModelPolicyRequest(Allowed(policy.AllowedModelsJson), Limits(policy.DailyTokenLimitsJson))
                : new()).ToArray(),
            new(Allowed(personal?.AllowedModelsJson), overrides));

        // 計算當日 UTC 日曆日區間（00:00:00Z ~ 隔日 00:00:00Z）
        var start = UtcDay.Start(asOf ?? clock.GetUtcNow());
        var end = start.AddDays(1);

        // 對話生成與其他背景任務都會消耗同一份每日配額，因此兩邊的用量要合併計算
        var chatRunsQuery = db.Runs
            .AsNoTracking()
            .Where(x => x.OwnerId == owner && x.CreatedAt >= start && x.CreatedAt < end)
            .Select(x => new
            {
                x.ModelId,
                x.InputTokens,
                x.OutputTokens,
                x.ReservedTokens,
                Active = x.ActiveOwnerId != null
            });

        var taskInvocationsQuery = db.Set<ModelInvocation>()
            .AsNoTracking()
            .Where(x => x.OwnerId == owner && x.CreatedAt >= start && x.CreatedAt < end && x.Kind != "embedding" && x.Kind != "web-search")
            .Select(x => new
            {
                x.ModelId,
                x.InputTokens,
                x.OutputTokens,
                x.ReservedTokens,
                Active = x.Status == "running"
            });

        var usage = await chatRunsQuery
            .Concat(taskInvocationsQuery)
            .GroupBy(x => x.ModelId)
            .Select(g => new
            {
                ModelId = g.Key,
                // 已結算的用量
                Used = g.Sum(x => !x.Active && x.InputTokens != null && x.OutputTokens != null ? x.InputTokens.Value + x.OutputTokens.Value : 0),
                // 尚未結算或執行中的保留用量
                Reserved = g.Sum(x => x.Active || x.InputTokens == null || x.OutputTokens == null ? x.ReservedTokens : 0)
            })
            .ToDictionaryAsync(x => x.ModelId, ct);

        string PublicIdOf(string id) => publicIds ? presentation.PublicId(id) : id;

        var budgets = inference.Value.Models.Select(model =>
        {
            long? limit = effective.DailyTokenLimits.TryGetValue(model.Id, out var cap) ? cap : null;
            var value = usage.GetValueOrDefault(model.Id);
            var used = value?.Used ?? 0;
            var reserved = value?.Reserved ?? 0;
            var remaining = limit is long maximum ? (long?)Math.Max(0, maximum - used - reserved) : null;
            var source = overrides.ContainsKey(model.Id) ? "personal" : limit is not null ? "group" : "unlimited";

            return new ModelTokenBudgetDto(
                PublicIdOf(model.Id),
                limit,
                used,
                reserved,
                remaining,
                source,
                presentation.DisplayName(model.Id, administrator: !publicIds));
        }).ToArray();

        return new(
            effective.AllowedModelIds?.Select(PublicIdOf).ToArray(),
            policies.Select(x => x.StoredAttachmentLimitBytes).Min(),
            budgets,
            end);
    }

    /// <summary>
    /// 檢查使用者是否有權限使用該模型，且尚有剩餘 Token 配額。
    /// </summary>
    public async Task<Result> RequireAsync(Guid owner, string internalModel, CancellationToken ct, bool checkQuota = true)
    {
        var value = await ForAsync(owner, ct, publicIds: false);
        if (!Allows(value, internalModel)) return InferenceErrors.ModelGroupForbidden;
        if (checkQuota && value.Models.FirstOrDefault(x => x.ModelId == internalModel)?.RemainingTokens is <= 0)
        {
            return InferenceErrors.ModelTokenQuota;
        }

        return Result.Success;
    }

    /// <summary>
    /// 根據當日剩餘 Token 配額調整生成參數。
    /// 必須在使用者的行鎖（owner row-lock）交易內呼叫，且預扣值要在釋放鎖之前寫入資料庫；
    /// 否則並行請求會同時通過檢查，合計超出配額。
    /// </summary>
    public async Task<Result<GenerationParameters>> BudgetAsync(
        Guid owner,
        string model,
        GenerationParameters parameters,
        long inputEstimate,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        var value = await ForAsync(owner, ct, publicIds: false, asOf: createdAt);
        if (!Allows(value, model)) return InferenceErrors.ModelGroupForbidden;

        var remaining = value.Models.First(x => x.ModelId == model).RemainingTokens;
        var output = remaining is long cap ? Math.Min(parameters.MaxOutputTokens, cap - inputEstimate) : parameters.MaxOutputTokens;
        if (output < 1) return InferenceErrors.ModelTokenQuota;

        // 輸出上限縮減多少，ContextTokens 就跟著縮減多少，讓輸入可用的空間維持不變
        return parameters with
        {
            MaxOutputTokens = (int)output,
            ContextTokens = parameters.ContextTokens - parameters.MaxOutputTokens + (int)output
        };
    }

    private static bool Allows(EffectiveModelPolicyDto policy, string model) =>
        policy.AllowedModelIds is null || policy.AllowedModelIds.Contains(model);
}
