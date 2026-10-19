using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Inference.ModelPolicies;

/// <summary>
/// 儲存使用者的個人模型白名單與 Token 上限；策略為空時刪除該列。會稽核。策略規則在稽核交易內檢查（不走 request validator），
/// 讓被拒絕的變更同樣留下稽核紀錄。
/// </summary>
internal sealed class SetUserModelPolicy(AppDbContext db, AdministrativeAudit audit, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/model-policy", async (Guid id, ModelPolicyRequest body, ICurrentUser user, SetUserModelPolicy handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("SetUserModelPolicy");

    public Task<Result> HandleAsync(Guid actor, Guid id, ModelPolicyRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.IdentityUsers, "admin.user_model_policy", id, id.ToString(), async token => await policies.PersonalAsync(id, token), async () =>
    {
        if (policies.Check(request) is { } invalid) return invalid;
        // 用一次不改值的 UPDATE 鎖住使用者列，讓同一使用者的策略變更互相序列化；WHERE 條件同時確認使用者未被刪除。
        var locked = await db.Users.Where(x => x.Id == id && x.DeletedAt == null)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
        if (locked != 1) return InferenceErrors.AdminResourceNotFound;
        var value = await db.Set<UserModelPolicy>().FindAsync([id], ct);
        if (value is null) { value = new() { UserId = id }; db.Add(value); }
        value.AllowedModelsJson = ModelPolicyService.SerializeAllowed(request.AllowedModelIds);
        value.DailyTokenLimitsJson = ModelPolicyService.SerializeLimits(request.DailyTokenLimits);
        if (value.AllowedModelsJson is null && value.DailyTokenLimitsJson is null) db.Remove(value);
        return Result.Success;
    }, ct);
}
