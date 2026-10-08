using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

/// <summary>Everything listed up to <see cref="Through"/>; later arrivals stay unread.</summary>
public sealed record ReadNotificationsRequest(DateTimeOffset Through);

internal sealed class ReadNotificationsRequestValidator : RequestValidator<ReadNotificationsRequest>
{
    public override string ProblemCode => "notification_cursor_required";

    public ReadNotificationsRequestValidator() => RuleFor(x => x.Through).NotEmpty();
}

internal static class ReadNotifications
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("/read", async (ReadNotificationsRequest body, ICurrentUser user, NexusDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            await Unread(db, user.Id).Where(x => x.CreatedAt <= body.Through).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct);
            return TypedResults.NoContent();
        }).WithName("ReadNotifications");
        routes.MapPost("/{id:guid}/read", async (Guid id, ICurrentUser user, NexusDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            await Unread(db, user.Id).Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct);
            return TypedResults.NoContent();
        }).WithName("ReadNotification");
    }

    private static IQueryable<WorkspaceNotification> Unread(NexusDbContext db, Guid owner) => db.Set<WorkspaceNotification>().Inbox(owner).Where(x => x.ReadAt == null);
}
