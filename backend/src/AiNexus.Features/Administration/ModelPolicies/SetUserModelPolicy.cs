using AiNexus.Features.AccessControl.ModelPolicies;
using AiNexus.Features.Inference.ModelPolicies;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.ModelPolicies;

/// <summary>
/// 儲存使用者的個人模型白名單與 Token 上限；策略為空時刪除該列。會稽核。策略規則在稽核交易內檢查（不走 request validator），
/// 讓被拒絕的變更同樣留下稽核紀錄。
/// </summary>
internal sealed class SetUserModelPolicy(NexusDbContext db, AdministrativeAudit audit, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/model-policy", async (Guid id, ModelPolicyRequest body, SetUserModelPolicy handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SetUserModelPolicy");

    public Task<Result> HandleAsync(Guid id, ModelPolicyRequest request, CancellationToken ct) => audit.MutateAsync("admin.user_model_policy", id, id.ToString(), async () =>
    {
        if (policies.Check(request) is { } invalid) return invalid;
        // 以無實質變更的 UPDATE 取得該使用者的列鎖，同時確認使用者未被刪除。
        if (await db.Users.Where(x => x.Id == id && x.DeletedAt == null).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct) != 1) return AdministrationErrors.NotFound;
        var value = await db.Set<UserModelPolicy>().FindAsync([id], ct);
        if (value is null) { value = new() { UserId = id }; db.Add(value); }
        value.AllowedModelsJson = ModelPolicyService.SerializeAllowed(request.AllowedModelIds);
        value.DailyTokenLimitsJson = ModelPolicyService.SerializeLimits(request.DailyTokenLimits);
        if (value.AllowedModelsJson is null && value.DailyTokenLimitsJson is null) db.Remove(value);
        return Result.Success;
    }, ct);
}
