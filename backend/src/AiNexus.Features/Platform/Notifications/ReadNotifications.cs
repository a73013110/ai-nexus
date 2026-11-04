using System.Linq.Expressions;
using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>標為已讀的截止時間（含）；之後才到的通知維持未讀。</summary>
public sealed record ReadNotificationsRequest(DateTimeOffset Through);

internal sealed class ReadNotificationsRequestValidator : RequestValidator<ReadNotificationsRequest>
{
    public override Error Problem => NotificationsErrors.CursorRequired;

    public ReadNotificationsRequestValidator() => RuleFor(x => x.Through).NotEmpty();
}

/// <summary>將使用者的通知標為已讀：截止時間（含）之前的全部，或單一 id。</summary>
internal sealed class ReadNotifications(AppDbContext db, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapPost("/read", (ReadNotificationsRequest body, ICurrentUser user, ReadNotifications handler, CancellationToken ct) =>
            handler.ThroughAsync(user.Id, body.Through, ct))
            .NotAudited("使用者自己的通知").WithName("ReadNotifications");
        routes.MapPost("/{id:guid}/read", (Guid id, ICurrentUser user, ReadNotifications handler, CancellationToken ct) =>
            handler.OneAsync(user.Id, id, ct))
            .NotAudited("使用者自己的通知").WithName("ReadNotification");
    }

    public Task<Result> ThroughAsync(Guid owner, DateTimeOffset through, CancellationToken ct) => MarkReadAsync(owner, x => x.CreatedAt <= through, ct);

    public Task<Result> OneAsync(Guid owner, Guid id, CancellationToken ct) => MarkReadAsync(owner, x => x.Id == id, ct);

    // 只更新收件匣中仍未讀的列，已讀時間不會被重複呼叫覆寫。
    private async Task<Result> MarkReadAsync(Guid owner, Expression<Func<Notification, bool>> which, CancellationToken ct)
    {
        await db.Notifications.Inbox(owner)
            .Where(x => x.ReadAt == null).Where(which).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct);
        return Result.Success;
    }
}
