using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

internal static class DismissNotification
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, NexusDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            await db.Set<WorkspaceNotification>().Where(x => x.Id == id && x.OwnerId == user.Id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.DismissedAt, clock.GetUtcNow()), ct);
            return TypedResults.NoContent();
        })
        .WithName("DismissNotification");
}
