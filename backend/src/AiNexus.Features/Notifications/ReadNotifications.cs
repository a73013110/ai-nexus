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

/// <summary>Marks the user's notifications read, all of them up to a time or one by id.</summary>
internal sealed class ReadNotifications(NexusDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapPost("/read", async (ReadNotificationsRequest body, ICurrentUser user, ReadNotifications handler, CancellationToken ct) =>
        {
            await handler.ThroughAsync(user.Id, body.Through, ct);
            return TypedResults.NoContent();
        }).WithName("ReadNotifications");
        routes.MapPost("/{id:guid}/read", async (Guid id, ICurrentUser user, ReadNotifications handler, CancellationToken ct) =>
        {
            await handler.OneAsync(user.Id, id, ct);
            return TypedResults.NoContent();
        }).WithName("ReadNotification");
    }

    public Task ThroughAsync(Guid owner, DateTimeOffset through, CancellationToken ct) => Unread(owner).Where(x => x.CreatedAt <= through).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct);

    public Task OneAsync(Guid owner, Guid id, CancellationToken ct) => Unread(owner).Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct);

    private IQueryable<WorkspaceNotification> Unread(Guid owner) => db.Set<WorkspaceNotification>().Inbox(owner).Where(x => x.ReadAt == null);
}
