using System.Data;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.ModelPolicies;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.AccessControl.Roles;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference.ModelPolicies;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

/// <summary>所有行政變更與其前後快照共用同一個交易，變更與稽核要嘛一起成功、要嘛一起回滾。</summary>
public sealed class AdministrativeAudit(NexusDbContext db, CurrentUser current, AccessService access, AdministrativeWriteLock writes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>存取管理類變更，權限要求為 <see cref="AdministrationConfiguration.Feature"/>。</summary>
    internal Task<Result> MutateAsync(string action, Guid? resource, string key, Func<Task<Result>> mutation, CancellationToken ct)
        => MutateAsync(AdministrationConfiguration.Feature, action, resource, key, mutation, ct);

    /// <summary>
    /// 在行政寫入鎖與 Serializable 交易內執行變更，並於交易內前後各檢一次 <paramref name="feature"/> 權限（不採用請求開始時的授權快取）。
    /// 任何失敗（回傳錯誤或 <see cref="ExternalServiceException"/>）都會回滾，並以錯誤代碼寫入稽核。
    /// </summary>
    internal async Task<Result> MutateAsync(string feature, string action, Guid? resource, string key, Func<Task<Result>> mutation, CancellationToken ct)
    {
        var signedIn = await current.GetAsync(ct);
        if (!signedIn.IsSuccess) return signedIn.Error;
        var actor = signedIn.Value.Id;
        await writes.Gate.WaitAsync(ct);
        object? before = null;
        try
        {
            Error? failure;
            try { failure = await CommitAsync(); }
            catch (ExternalServiceException error) { await RecordFailureAsync(error.Error.Code); throw; }
            if (failure is null) return Result.Success;
            await RecordFailureAsync(failure.Code);
            return failure;
        }
        finally { writes.Gate.Release(); }

        async Task<Error?> CommitAsync()
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // 兩次權限檢查都在交易內讀 SQL，不信任請求一開始授權時的快取結果。
            access.Invalidate();
            if (!await HoldsAsync(actor, feature, ct)) return AccessControlErrors.FeatureForbidden;
            before = await SnapshotAsync(action, resource, key, ct);
            var outcome = await mutation();
            if (!outcome.IsSuccess) return outcome.Error;
            await db.SaveChangesAsync(ct);
            access.Invalidate();
            if (!await HoldsAsync(actor, feature, ct)) return AdministrationErrors.Lockout;
            var after = await SnapshotAsync(action, resource, key, ct);
            db.AuditEvents.Add(new()
            {
                OwnerId = actor,
                ResourceId = resource,
                Action = action,
                Result = "saved",
                DetailsJson = JsonSerializer.Serialize(new { resourceKey = key, before, after }, Json)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        // 交易已回滾後才會執行。先清空 ChangeTracker，避免交易中尚未提交的授權變更被一併寫入稽核。
        async Task RecordFailureAsync(string code)
        {
            db.ChangeTracker.Clear(); access.Invalidate();
            db.AuditEvents.Add(new()
            {
                OwnerId = actor,
                ResourceId = resource,
                Action = action,
                Result = code,
                DetailsJson = JsonSerializer.Serialize(new { resourceKey = key[..Math.Min(key.Length, 64)], before, failureCode = code }, Json)
            });
            await db.SaveChangesAsync(ct);
        }
    }
    private async Task<bool> HoldsAsync(Guid actor, string feature, CancellationToken ct)
        => (await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == feature);
    private async Task<object?> SnapshotAsync(string action, Guid? resource, string key, CancellationToken ct)
    {
        if (action.StartsWith("admin.embedding_", StringComparison.Ordinal) && int.TryParse(key, out var profile)) return new {
            profiles = await db.Set<EmbeddingProfile>().AsNoTracking().Select(x => new { x.Id, x.Status, x.ActivatedAt, x.RetiredAt }).ToArrayAsync(ct),
            vectors = await db.Set<ChunkEmbedding768>().CountAsync(x => x.ProfileId == profile, ct) + await db.Set<ChunkEmbedding1024>().CountAsync(x => x.ProfileId == profile, ct)
        };
        if (action == "admin.user_model_policy")
        {
            var policy = await db.Set<UserModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == resource, ct);
            return new ModelPolicyRequest(ModelPolicyService.Allowed(policy?.AllowedModelsJson), ModelPolicyService.Limits(policy?.DailyTokenLimitsJson));
        }
        if (action == "admin.user_storage") return await db.Users.AsNoTracking().Where(x => x.Id == resource).Select(x => new { x.AttachmentLimitBytes }).SingleOrDefaultAsync(ct);
        if (action is "admin.user" or "admin.user_delete")
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == resource, ct);
            if (user is null) return null;
            var roleIds = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == resource).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(ct);
            return new
            {
                user.Account,
                user.DisplayName,
                user.Enabled,
                user.DeletedAt,
                user.SecurityVersion,
                authentication = UserAccounts.Authentication(user),
                roleIds
            };
        }
        if (action == "admin.user_roles") return new { roleIds = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == resource).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(ct) };
        if (action == "admin.role")
        {
            var value = await db.Set<Role>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == key, ct);
            return value is null ? null : new { value.Name, value.Enabled, groupIds = await db.Set<RoleGroupRole>().AsNoTracking().Where(x => x.RoleId == key).OrderBy(x => x.GroupId).Select(x => x.GroupId).ToArrayAsync(ct) };
        }
        if (action == "admin.group")
        {
            var value = await db.Set<RoleGroup>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == key, ct);
            var policy = await db.Set<GroupModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == key, ct);
            return value is null ? null : new
            {
                value.Name,
                value.Enabled,
                featureIds = await db.Set<RoleGroupFeature>().AsNoTracking().Where(x => x.GroupId == key).OrderBy(x => x.FeatureId).Select(x => x.FeatureId).ToArrayAsync(ct),
                policy = policy is null ? null : new GroupPolicyRequest(ModelPolicyService.Allowed(policy.AllowedModelsJson), ModelPolicyService.Limits(policy.DailyTokenLimitsJson), policy.StoredAttachmentLimitBytes)
            };
        }
        if (action.StartsWith("admin.navigation_group", StringComparison.Ordinal))
        {
            var group = await db.Set<NavigationGroup>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == key, ct);
            return group is null ? null : new { group.Name, group.ParentId, group.SortOrder };
        }
        var feature = await db.Set<Feature>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == key, ct);
        return feature is null ? null : new { feature.Name, feature.Enabled, feature.SortOrder, feature.GroupId, feature.Icon };
    }
}
