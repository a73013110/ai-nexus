using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Operations;

public sealed record GroupPolicyRequest(IReadOnlyList<string>? AllowedModelIds = null, IReadOnlyDictionary<string, long>? DailyTokenLimits = null, long? StoredAttachmentLimitBytes = null)
{
    /// <summary>資料列轉回請求形狀；目錄回應與稽核快照共用，兩邊才不會各自漏欄位。</summary>
    internal static GroupPolicyRequest From(GroupModelPolicy row)
        => new(ModelPolicyService.Allowed(row.AllowedModelsJson), ModelPolicyService.Limits(row.DailyTokenLimitsJson), row.StoredAttachmentLimitBytes);
}

/// <summary>
/// <c>Scopes</c>：功能 ID → 所有成員的資料範圍，只放目錄有宣告範圍的功能，而且每個這類功能都要有（ADR 0017）。
/// <c>ManagerScopes</c>：功能 ID → 主管另可看的範圍，只放目錄有宣告主管範圍、而且要另外給主管的功能；不列＝不另外（ADR 0022）。
/// <c>RowVersion</c> 是修改既有群組時讀到的版本；新增時為 null。
/// </summary>
[ValidatedInHandler("Format failures return the specific codes of AccessRules, checked in the handler before the transaction.")]
public sealed record GroupUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy = null, IReadOnlyDictionary<string, string>? Scopes = null,
    IReadOnlyDictionary<string, string>? ManagerScopes = null, long? RowVersion = null);

/// <summary>建立或更新群組、其功能（含資料範圍）與模型策略。會稽核；若會移除執行者自己的群組管理權限則拒絕。</summary>
internal sealed class SaveRoleGroup(AppDbContext db, AdministrativeWrite write, ModelPolicyService policies) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/groups/{id}", (string id, GroupUpdateRequest body, ICurrentUser user, SaveRoleGroup handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct))
        .Audited(AccessControlAudit.GroupUpdated)
        .WithName("SaveRoleGroup");

    public async Task<Result> HandleAsync(Guid actor, string id, GroupUpdateRequest request, CancellationToken ct)
    {
        if ((AccessRules.Key(id) ?? AccessRules.Name(request.Name) ?? AccessRules.FeatureKeys(request.FeatureIds) ?? AccessRules.Scopes(request.FeatureIds, request.Scopes, request.ManagerScopes)) is { } invalid) return invalid;
        if (request.Policy is { } policy)
        {
            if (policies.Check(new(policy.AllowedModelIds, policy.DailyTokenLimits)) is { } rejected) return rejected;
            if (policy.StoredAttachmentLimitBytes is < 0 or > FilesOptions.MaximumLimitBytes) return InferenceErrors.InvalidModelPolicy;
        }

        var begun = await write.BeginAsync(AuditTarget.Of(id, token => SnapshotAsync(id, token)).KeepingAccess(actor, FeatureCatalog.AccessControlGroups), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        if (await AccessRules.ExistingAsync(db.Features.Select(x => x.Id), request.FeatureIds, ct) is { } unknown) return unknown;
        var group = await db.RoleGroups.FindAsync([id], ct);
        // 新增不帶版本、修改一定帶：同一個 id 剛被別人建立或刪除時，不會悄悄變成另一種操作。
        if ((group is null) != (request.RowVersion is null)) return AccessControlErrors.WriteConflict;
        if (group is null) { group = new() { Id = id }; db.Add(group); }
        else db.ExpectRowVersion(group, request.RowVersion!.Value);
        group.Name = request.Name.Trim(); group.Enabled = request.Enabled;
        var old = await db.RoleGroupFeatures.Where(x => x.GroupId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !request.FeatureIds.Contains(x.FeatureId)));
        var added = request.FeatureIds.Where(x => !old.Any(y => y.FeatureId == x)).Select(x => new RoleGroupFeature { GroupId = id, FeatureId = x }).ToList();
        db.AddRange(added);
        foreach (var grant in old.Where(x => request.FeatureIds.Contains(x.FeatureId)).Concat(added))
        {
            grant.Scope = request.Scopes?.GetValueOrDefault(grant.FeatureId);
            grant.ManagerScope = request.ManagerScopes?.GetValueOrDefault(grant.FeatureId);
        }
        var value = await db.GroupModelPolicies.FindAsync([id], ct);
        if (request.Policy is null) { if (value is not null) db.Remove(value); }
        else
        {
            if (value is null) { value = new() { GroupId = id }; db.Add(value); }
            value.AllowedModelsJson = ModelPolicyService.SerializeAllowed(request.Policy.AllowedModelIds);
            value.DailyTokenLimitsJson = ModelPolicyService.SerializeLimits(request.Policy.DailyTokenLimits); value.StoredAttachmentLimitBytes = request.Policy.StoredAttachmentLimitBytes;
        }

        return await transaction.CommitAsync(ct);
    }

    private async Task<object?> SnapshotAsync(string id, CancellationToken ct)
    {
        var value = await db.RoleGroups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null) return null;
        var policy = await db.GroupModelPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == id, ct);
        var grants = await db.RoleGroupFeatures.AsNoTracking().Where(x => x.GroupId == id).OrderBy(x => x.FeatureId).ToListAsync(ct);
        return new
        {
            value.Name,
            value.Enabled,
            featureIds = grants.Select(x => x.FeatureId).ToArray(),
            // 稽核遮罩只保留白名單欄位名稱，所以範圍寫成清單而不是以功能 ID 為鍵的物件。
            scopes = grants.Where(x => x.Scope != null || x.ManagerScope != null).Select(x => new { featureId = x.FeatureId, scope = x.Scope, managerScope = x.ManagerScope }).ToArray(),
            policy = policy is null ? null : GroupPolicyRequest.From(policy)
        };
    }
}
