using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Ordering;

/// <summary>清除所有人的排序，回到依姓名、帳號排列。稽核保留清除前的值，誤按時可據以還原。</summary>
internal sealed class ResetUserOrder(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/users/order", (ResetUserOrder handler, CancellationToken ct) =>
            handler.HandleAsync(ct))
        .Audited(IdentityAudit.UserReordered)
        .WithName("ResetUserOrder");

    public async Task<Result> HandleAsync(CancellationToken ct)
    {
        var begun = await write.BeginAsync(AuditTarget.Of("order", token => UserOrderSnapshot.OfAsync(db, null, token)).Locking(AccessControlLocks.Authorization), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        await db.Users.Where(x => x.SortOrder != null).ExecuteUpdateAsync(x => x.SetProperty(u => u.SortOrder, (int?)null), ct);

        return await transaction.CommitAsync(ct);
    }
}
