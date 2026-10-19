using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Operations;

public sealed record GroupPolicyRequest(IReadOnlyList<string>? AllowedModelIds = null, IReadOnlyDictionary<string, long>? DailyTokenLimits = null, long? StoredAttachmentLimitBytes = null)
{
    /// <summary>資料列轉回請求形狀；目錄回應與稽核快照共用，兩邊才不會各自漏欄位。</summary>
    internal static GroupPolicyRequest From(GroupModelPolicy row)
        => new(ModelPolicyService.Allowed(row.AllowedModelsJson), ModelPolicyService.Limits(row.DailyTokenLimitsJson), row.StoredAttachmentLimitBytes);
}

/// <summary><c>Scopes</c>：功能 ID → 資料範圍，只放目錄有宣告範圍的功能，而且每個這類功能都要有（ADR 0017）。</summary>
[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record GroupUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy = null, IReadOnlyDictionary<string, string>? Scopes = null);

/// <summary>建立或更新群組、其功能（含資料範圍）與模型策略。會稽核；若會移除執行者自己的群組管理權限則拒絕。</summary>
internal sealed class SaveRoleGroup(AppDbContext db, AdministrativeAudit audit, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/groups/{id}", async (string id, GroupUpdateRequest body, ICurrentUser user, SaveRoleGroup handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("SaveRoleGroup");

    public Task<Result> HandleAsync(Guid actor, string id, GroupUpdateRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.AccessControlGroups, "admin.group", null, id, token => SnapshotAsync(id, token), async () =>
    {
        if ((AccessRules.Key(id) ?? AccessRules.Name(request.Name) ?? AccessRules.FeatureKeys(request.FeatureIds) ?? AccessRules.Scopes(request.FeatureIds, request.Scopes)) is { } invalid) return invalid;
        if (request.Policy is { } policy)
        {
            if (policies.Check(new(policy.AllowedModelIds, policy.DailyTokenLimits)) is { } rejected) return rejected;
            if (policy.StoredAttachmentLimitBytes is < 0 or > FilesOptions.MaximumLimitBytes) return InferenceErrors.InvalidModelPolicy;
        }
        if (await AccessRules.ExistingAsync(db.Set<Feature>().Select(x => x.Id), request.FeatureIds, ct) is { } unknown) return unknown;
        var group = await db.Set<RoleGroup>().FindAsync([id], ct);
        if (group is null) { group = new() { Id = id }; db.Add(group); }
        group.Name = request.Name.Trim(); group.Enabled = request.Enabled;
        var old = await db.Set<RoleGroupFeature>().Where(x => x.GroupId == id).ToListAsync(ct);
        var scopes = request.Scopes ?? new Dictionary<string, string>();
        db.RemoveRange(old.Where(x => !request.FeatureIds.Contains(x.FeatureId)));
        foreach (var kept in old.Where(x => request.FeatureIds.Contains(x.FeatureId))) kept.Scope = scopes.GetValueOrDefault(kept.FeatureId);
        db.AddRange(request.FeatureIds.Where(x => !old.Any(y => y.FeatureId == x)).Select(x => new RoleGroupFeature { GroupId = id, FeatureId = x, Scope = scopes.GetValueOrDefault(x) }));
        var value = await db.Set<GroupModelPolicy>().FindAsync([id], ct);
        if (request.Policy is null) { if (value is not null) db.Remove(value); }
        else
        {
            if (value is null) { value = new() { GroupId = id }; db.Add(value); }
            value.AllowedModelsJson = ModelPolicyService.SerializeAllowed(request.Policy.AllowedModelIds);
            value.DailyTokenLimitsJson = ModelPolicyService.SerializeLimits(request.Policy.DailyTokenLimits); value.StoredAttachmentLimitBytes = request.Policy.StoredAttachmentLimitBytes;
        }
        return Result.Success;
    }, ct);

    private async Task<object?> SnapshotAsync(string id, CancellationToken ct)
    {
        var value = await db.Set<RoleGroup>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null) return null;
        var policy = await db.Set<GroupModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == id, ct);
        var grants = await db.Set<RoleGroupFeature>().AsNoTracking().Where(x => x.GroupId == id).OrderBy(x => x.FeatureId).ToListAsync(ct);
        return new
        {
            value.Name,
            value.Enabled,
            featureIds = grants.Select(x => x.FeatureId).ToArray(),
            // 稽核遮罩只保留白名單欄位名稱，所以範圍寫成清單而不是以功能 ID 為鍵的物件。
            scopes = grants.Where(x => x.Scope != null).Select(x => new { featureId = x.FeatureId, scope = x.Scope }).ToArray(),
            policy = policy is null ? null : GroupPolicyRequest.From(policy)
        };
    }
}
