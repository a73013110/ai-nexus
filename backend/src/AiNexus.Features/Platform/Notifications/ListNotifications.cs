using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

public sealed record NotificationTargetDto(string Kind, Guid Id);

public sealed record NotificationDto(Guid Id, int Version, string Type, string Severity, string Title, string Body,
    NotificationTargetDto Target, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, string? IssueCode = null);

public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Unread, bool HasMore);

internal sealed class ListNotifications(AppDbContext db)
{
    private const int PageSize = 50;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", (bool? unread, Guid? before, ICurrentUser user, ListNotifications handler, CancellationToken ct) => handler.HandleAsync(user.Id, unread ?? false, before, ct).ToHttpResultAsync())
        .NotAudited("使用者自己的通知")
        .WithName("ListNotifications");

    public async Task<Result<NotificationPageDto>> HandleAsync(Guid owner, bool unread, Guid? before, CancellationToken ct)
    {
        var inbox = db.Set<Notification>().AsNoTracking().Inbox(owner);
        var count = await inbox.CountAsync(x => x.ReadAt == null, ct);
        var query = inbox.Where(x => !unread || x.ReadAt == null);
        if (before is { } cursor)
        {
            var previous = await inbox.SingleOrDefaultAsync(x => x.Id == cursor, ct);
            if (previous is null) return NotificationsErrors.CursorInvalid;
            query = query.Where(x => x.CreatedAt < previous.CreatedAt || (x.CreatedAt == previous.CreatedAt && x.Id.CompareTo(cursor) < 0));
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(PageSize + 1)
            .Select(x => new NotificationDto(x.Id, x.Version, x.Type, x.Severity, NotificationDisplay.Title(x.Severity, x.Title),
                NotificationDisplay.Body(x.Severity, x.Body, x.IssueCode), new(x.TargetKind, x.TargetId), x.CreatedAt, x.ReadAt, x.IssueCode))
            .ToListAsync(ct);
        return new NotificationPageDto(rows.Take(PageSize).ToArray(), count, rows.Count > PageSize);
    }
}
