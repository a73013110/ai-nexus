using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed record AdminUserModelPolicyDto(ModelPolicyRequest Personal, EffectiveModelPolicyDto Effective);

/// <summary>A user's personal policy and the effective result, with internal model ids.</summary>
internal sealed class GetUserModelPolicy(NexusDbContext db, ModelPolicyService policies)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/model-policy", async (Guid id, GetUserModelPolicy handler, CancellationToken ct) => (await handler.HandleAsync(id, ct)).ToHttpResult())
        .WithName("GetUserModelPolicy").Produces<AdminUserModelPolicyDto>();

    public async Task<Result<AdminUserModelPolicyDto>> HandleAsync(Guid owner, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == owner && x.DeletedAt == null, ct)) return AdministrationErrors.NotFound;
        var personal = await db.Set<UserModelPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner, ct);
        return new AdminUserModelPolicyDto(new(ModelPolicyService.Allowed(personal?.AllowedModelsJson), ModelPolicyService.Limits(personal?.DailyTokenLimitsJson)), await policies.ForAsync(owner, ct, publicIds: false));
    }
}
