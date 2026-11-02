using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Inference.ModelPolicies;

/// <summary>
/// 儲存使用者的個人模型白名單與 Token 上限；策略為空時刪除該列。會稽核。策略規則在 handler 內檢查（不走 request validator），才能回策略專屬的錯誤代碼。
/// 只動一列、單一意圖：後寫的贏，前後值都在稽核，所以不帶版本也不取鎖；同時第一次新增撞上主鍵時回寫入衝突。
/// </summary>
internal sealed class SetUserModelPolicy(AppDbContext db, AdministrativeWrite write, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/model-policy", (Guid id, ModelPolicyRequest body, SetUserModelPolicy handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .Audited(InferenceAudit.UserPolicyUpdated)
        .WithName("SetUserModelPolicy");

    public async Task<Result> HandleAsync(Guid id, ModelPolicyRequest request, CancellationToken ct)
    {
        if (policies.Check(request) is { } invalid) return invalid;

        var begun = await write.BeginAsync(AuditTarget.Of(id, async token => await policies.PersonalAsync(id, token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        if (!await db.Users.AnyAsync(x => x.Id == id && x.DeletedAt == null, ct)) return InferenceErrors.AdminResourceNotFound;
        var value = await db.UserModelPolicies.FindAsync([id], ct);
        if (value is null) { value = new() { UserId = id }; db.Add(value); }
        value.AllowedModelsJson = ModelPolicyService.SerializeAllowed(request.AllowedModelIds);
        value.DailyTokenLimitsJson = ModelPolicyService.SerializeLimits(request.DailyTokenLimits);
        if (value.AllowedModelsJson is null && value.DailyTokenLimitsJson is null) db.Remove(value);

        return await transaction.CommitAsync(ct);
    }
}
