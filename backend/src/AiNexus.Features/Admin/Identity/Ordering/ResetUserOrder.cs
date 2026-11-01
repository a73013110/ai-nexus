using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Ordering;

/// <summary>清除所有人的排序，回到依姓名、帳號排列。稽核保留清除前的值，誤按時可據以還原。</summary>
internal sealed class ResetUserOrder(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/users/order", (IRequestActor actor, ResetUserOrder handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, ct))
        .Audited(IdentityAudit.UserReordered)
        .WithName("ResetUserOrder");

    public Task<Result> HandleAsync(Guid actor, CancellationToken ct)
        => write.ExecuteAsync(actor, AuditTarget.Of(FeatureCatalog.IdentityUsers, "order", token => UserOrderSnapshot.OfAsync(db, null, token)), async () =>
        {
            // 在 AdministrativeWrite 的交易內執行；第二次權限檢查失敗時隨交易回滾。
            await db.Users.Where(x => x.SortOrder != null).ExecuteUpdateAsync(x => x.SetProperty(u => u.SortOrder, (int?)null), ct);
            return Result.Success;
        }, ct);
}
