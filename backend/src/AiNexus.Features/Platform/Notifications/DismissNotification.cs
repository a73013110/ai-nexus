using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>隱藏使用者的一則通知；找不到時也視為成功，可重複呼叫。</summary>
internal sealed class DismissNotification(AppDbContext db, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DismissNotification handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .NotAudited("使用者自己的通知")
        .WithName("DismissNotification");

    public async Task<Result> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await db.Notifications.Where(x => x.Id == id && x.OwnerId == owner)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DismissedAt, clock.GetUtcNow()), ct);
        return Result.Success;
    }
}
