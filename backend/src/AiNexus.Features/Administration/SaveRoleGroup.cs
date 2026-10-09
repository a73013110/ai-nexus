using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed record GroupPolicyRequest(IReadOnlyList<string>? AllowedModelIds = null, IReadOnlyDictionary<string, long>? DailyTokenLimits = null, long? StoredAttachmentLimitBytes = null);

/// <summary>Not a request validator: format failures are audited inside the administrative transaction (see <see cref="AccessRules"/>).</summary>
public sealed record GroupUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy = null);

/// <summary>Creates or updates a group, its features and its model policy. Audited; refused when it would remove the actor's own administrator access.</summary>
internal sealed class SaveRoleGroup(NexusDbContext db, AdministrativeAudit audit, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/groups/{id}", async (string id, GroupUpdateRequest body, SaveRoleGroup handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SaveRoleGroup");

    public Task<Result> HandleAsync(string id, GroupUpdateRequest request, CancellationToken ct) => audit.TryMutateAsync("admin.group", null, id, async () =>
    {
        if ((AccessRules.Key(id) ?? AccessRules.Name(request.Name) ?? AccessRules.Keys(request.FeatureIds)) is { } invalid) return invalid;
        if (request.Policy is { } policy)
        {
            if (policies.Check(new(policy.AllowedModelIds, policy.DailyTokenLimits)) is { } rejected) return rejected;
            if (policy.StoredAttachmentLimitBytes is < 0 or > AttachmentOptions.MaximumLimitBytes) return InferenceErrors.InvalidModelPolicy;
        }
        if (await AccessRules.ExistingAsync(db.Set<Feature>().Select(x => x.Id), request.FeatureIds, ct) is { } unknown) return unknown;
        var group = await db.Set<RoleGroup>().FindAsync([id], ct);
        if (group is null) { group = new() { Id = id }; db.Add(group); }
        group.Name = request.Name.Trim(); group.Enabled = request.Enabled;
        var old = await db.Set<RoleGroupFeature>().Where(x => x.GroupId == id).ToListAsync(ct);
        db.RemoveRange(old.Where(x => !request.FeatureIds.Contains(x.FeatureId)));
        db.AddRange(request.FeatureIds.Where(x => !old.Any(y => y.FeatureId == x)).Select(x => new RoleGroupFeature { GroupId = id, FeatureId = x }));
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
}
