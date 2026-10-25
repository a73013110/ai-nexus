using System.Globalization;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Ordering;

/// <summary>移到整份名單（使用者順序）的第 <paramref name="Position"/> 位，從 1 起算；超過人數就放到最後。</summary>
public sealed record UserPositionRequest(int Position);

internal sealed class UserPositionRequestValidator : RequestValidator<UserPositionRequest>
{
    public override string ProblemCode => IdentityErrors.InvalidUserOrder.Code;

    public UserPositionRequestValidator() => RuleFor(x => x.Position).GreaterThanOrEqualTo(1);
}

/// <summary>
/// 使用者表上的拖曳、方向鍵或輸入編號：只傳「移到第幾位」，值由伺服器依整份名單計算（<see cref="ManualOrder"/>）。
/// 列表會分頁、搜尋，前端看不到完整順序，所以不讓前端算值。計算與寫入在同一個交易；會稽核。
/// </summary>
internal sealed class MoveUser(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/users/{id:guid}/position", async (Guid id, UserPositionRequest body, IRequestActor actor, MoveUser handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, body, ct)).ToHttpResult())
        .WithName("MoveUser");

    public async Task<Result> HandleAsync(Guid actor, Guid id, UserPositionRequest request, CancellationToken ct)
    {
        // 先算一次決定稽核要記哪些人；交易內重算，結果不同（期間有人改了順序）就拒絕，不寫半套。
        var planned = await PlanAsync(id, request.Position, ct);
        if (planned is null) return IdentityErrors.UserOrderStale;
        if (planned.Count == 0) return Result.Success;
        var ids = planned.Select(x => x.Id).ToArray();
        return await audit.MutateAsync(actor, FeatureCatalog.IdentityUsers, "admin.user_order", id, request.Position.ToString(CultureInfo.InvariantCulture), token => UserOrderSnapshot.OfAsync(db, ids, token), async () =>
        {
            var writes = await PlanAsync(id, request.Position, ct);
            if (writes is null || !writes.SequenceEqual(planned)) return IdentityErrors.UserOrderStale;
            var users = await db.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var (userId, sortOrder) in writes) users[userId].SortOrder = sortOrder;
            return Result.Success;
        }, ct);
    }

    /// <summary>移動所需的寫入；使用者不存在或已刪除時為 null。</summary>
    private async Task<IReadOnlyList<(Guid Id, int SortOrder)>?> PlanAsync(Guid id, int position, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(x => x.DeletedAt == null)
            .Select(x => new { x.Id, x.SortOrder, x.DisplayName, x.Account })
            .ToListAsync(ct);
        if (!users.Any(x => x.Id == id)) return null;
        var ordered = users.InUserOrder(x => x.SortOrder, x => x.DisplayName, x => x.Account).ThenBy(x => x.Id)
            .Select(x => (x.Id, x.SortOrder)).ToList();
        return ManualOrder.Plan(ordered, id, position - 1, UserOrder.MaxSortOrder);
    }
}
