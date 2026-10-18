using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>標為已讀的截止時間（含）；之後才到的通知維持未讀。</summary>
public sealed record ReadNotificationsRequest(DateTimeOffset Through);

internal sealed class ReadNotificationsRequestValidator : RequestValidator<ReadNotificationsRequest>
{
    public override string ProblemCode => "notification_cursor_required";

    public ReadNotificationsRequestValidator() => RuleFor(x => x.Through).NotEmpty();
}

/// <summary>將使用者的通知標為已讀：截止時間（含）之前的全部，或單一 id。</summary>
internal sealed class ReadNotifications(AppDbContext db, TimeProvider clock)
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

    private IQueryable<Notification> Unread(Guid owner) => db.Set<Notification>().Inbox(owner).Where(x => x.ReadAt == null);
}
