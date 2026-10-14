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

/// <summary>All administrative mutations and their before/after record share one transaction.</summary>
public sealed class AdministrativeAudit(NexusDbContext db, CurrentUser current, AccessService access, AdministrativeWriteLock writes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    /// <summary>
    /// An access-management change: <see cref="MutateAsync(string, string, Guid?, string, Func{Task{Result}}, CancellationToken)"/>
    /// requiring <see cref="AdministrationConfiguration.Feature"/>.
    /// </summary>
    internal Task<Result> MutateAsync(string action, Guid? resource, string key, Func<Task<Result>> mutation, CancellationToken ct)
        => MutateAsync(AdministrationConfiguration.Feature, action, resource, key, mutation, ct);

    /// <summary>
    /// Under the administrative write lock and one serializable transaction: the actor must still hold
    /// <paramref name="feature"/>, the before snapshot is taken, the mutation runs and is saved, the actor must keep that
    /// feature, and the after snapshot is audited. Any failure, returned or an <see cref="ExternalServiceException"/>,
    /// rolls back and is audited with its code.
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
            // Both checks read SQL inside this transaction, not the grants the request authorized with.
            access.Invalidate();
            if (!await HoldsAsync(actor, feature, ct)) return AccessControlErrors.FeatureForbidden;
            before = await SnapshotAsync(action, resource, key, ct);
            var outcome = await mutation();
            if (!outcome.IsSuccess) return outcome.Error;
            await db.SaveChangesAsync(ct);
            access.Invalidate();
            if (!await HoldsAsync(actor, feature, ct)) return AdministrationErrors.Lockout;
            var after = await SnapshotAsync(action, resource, key, ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource, Action = action, Result = "saved", DetailsJson = JsonSerializer.Serialize(new { resourceKey = key, before, after }, Json) });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return null;
        }

        // Runs after the failed transaction is disposed. Do not accidentally save pending grants.
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
            return user is null ? null : new { user.Account, user.DisplayName, user.Enabled, user.DeletedAt, user.SecurityVersion, authentication = UserAccounts.Authentication(user), roleIds = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == resource).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(ct) };
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
