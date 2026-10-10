using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

/// <summary>Hides one of the user's notifications; dismissing a missing one succeeds.</summary>
internal sealed class DismissNotification(NexusDbContext db, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, DismissNotification handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(user.Id, id, ct);
            return TypedResults.NoContent();
        })
        .WithName("DismissNotification");

    public Task HandleAsync(Guid owner, Guid id, CancellationToken ct) => db.Set<WorkspaceNotification>().Where(x => x.Id == id && x.OwnerId == owner)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.DismissedAt, clock.GetUtcNow()), ct);
}
