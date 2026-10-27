using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>隱藏使用者的一則通知；找不到時也視為成功，可重複呼叫。</summary>
internal sealed class DismissNotification(AppDbContext db, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, DismissNotification handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(user.Id, id, ct);
            return TypedResults.NoContent();
        })
        .NotAudited("使用者自己的通知")
        .WithName("DismissNotification");

    public Task HandleAsync(Guid owner, Guid id, CancellationToken ct) => db.Set<Notification>().Where(x => x.Id == id && x.OwnerId == owner)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.DismissedAt, clock.GetUtcNow()), ct);
}
