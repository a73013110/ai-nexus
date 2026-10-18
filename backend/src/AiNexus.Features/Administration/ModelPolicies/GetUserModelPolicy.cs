using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Inference.ModelPolicies;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.ModelPolicies;

public sealed record AdminUserModelPolicyDto(ModelPolicyRequest Personal, EffectiveModelPolicyDto Effective);

/// <summary>使用者的個人策略與有效結果，回傳內部模型 id（供管理端使用）。</summary>
internal sealed class GetUserModelPolicy(AppDbContext db, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/model-policy", async (Guid id, GetUserModelPolicy handler, CancellationToken ct) => (await handler.HandleAsync(id, ct)).ToHttpResult())
        .WithName("GetUserModelPolicy");

    public async Task<Result<AdminUserModelPolicyDto>> HandleAsync(Guid owner, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == owner && x.DeletedAt == null, ct)) return AdministrationErrors.NotFound;
        var personal = await db.Set<UserModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner, ct);
        var personalDto = new ModelPolicyRequest(ModelPolicyService.Allowed(personal?.AllowedModelsJson), ModelPolicyService.Limits(personal?.DailyTokenLimitsJson));
        var effective = await policies.ForAsync(owner, ct, publicIds: false);
        return new AdminUserModelPolicyDto(personalDto, effective);
    }
}
