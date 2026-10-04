using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Administration;

public sealed record EffectiveModelPolicyDto(IReadOnlyList<string>? AllowedModelIds, int? DailyRequestLimit, long? StoredAttachmentLimitBytes, int RequestsToday, DateTimeOffset ResetsAt);
public sealed class ModelPolicyService(NexusDbContext db, AccessService access, ModelPresentation presentation)
{
    public async Task<EffectiveModelPolicyDto> ForAsync(Guid owner, CancellationToken ct)
    {
        var grants = await access.ForUserAsync(owner, ct); var groups = grants.Groups.Select(x => x.Id).ToArray();
        var policies = await db.Set<GroupModelPolicy>().AsNoTracking().Where(x => groups.Contains(x.GroupId)).ToListAsync(ct);
        HashSet<string>? allowed = null;
        foreach (var policy in policies.Where(x => x.AllowedModelsJson is not null))
        {
            var ids = JsonSerializer.Deserialize<string[]>(policy.AllowedModelsJson!)!;
            if (allowed is null) allowed = ids.ToHashSet(StringComparer.Ordinal); else allowed.IntersectWith(ids);
        }
        DateTimeOffset start = DateTimeOffset.UtcNow.Date;
        var count = await db.Runs.CountAsync(x => x.OwnerId == owner && x.CreatedAt >= start, ct)
            + await db.Set<ModelInvocation>().CountAsync(x => x.OwnerId == owner && x.CreatedAt >= start && x.Kind != "embedding", ct);
        return new(allowed?.Select(presentation.PublicId).ToArray(), policies.Select(x => x.DailyRequestLimit).Min(),
            policies.Select(x => x.StoredAttachmentLimitBytes).Min(), count, start.AddDays(1));
    }
    public async Task RequireAsync(Guid owner, string internalModel, CancellationToken ct, bool checkQuota = true)
    {
        var value = await ForAsync(owner, ct);
        if (value.AllowedModelIds is not null && !value.AllowedModelIds.Contains(presentation.PublicId(internalModel)))
            throw new ApiException(403, "model_group_forbidden", "此模型不在你的群組授權範圍。");
        if (checkQuota && value.DailyRequestLimit is int limit && value.RequestsToday >= limit)
            throw new ApiException(429, "daily_quota", "今日生成次數已達群組配額。配額於 UTC 午夜重設。");
    }
}
