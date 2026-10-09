using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

public sealed record NotificationTargetDto(string Kind, Guid Id);

public sealed record NotificationDto(Guid Id, int Version, string Type, string Severity, string Title, string Body,
    NotificationTargetDto Target, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, string? IssueCode = null);

public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Unread, bool HasMore);

internal static class ListNotifications
{
    private const int PageSize = 50;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (bool? unread, Guid? before, ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
            (await HandleAsync(db, user.Id, unread ?? false, before, ct)).ToHttpResult())
        .WithName("ListNotifications")
        .Produces<NotificationPageDto>();

    public static async Task<Result<NotificationPageDto>> HandleAsync(NexusDbContext db, Guid owner, bool unread, Guid? before, CancellationToken ct)
    {
        var inbox = db.Set<WorkspaceNotification>().AsNoTracking().Inbox(owner);
        var count = await inbox.CountAsync(x => x.ReadAt == null, ct);
        var query = inbox.Where(x => !unread || x.ReadAt == null);
        if (before is { } cursor)
        {
            var previous = await inbox.SingleOrDefaultAsync(x => x.Id == cursor, ct);
            if (previous is null) return NotificationsErrors.CursorInvalid;
            query = query.Where(x => x.CreatedAt < previous.CreatedAt || (x.CreatedAt == previous.CreatedAt && x.Id.CompareTo(cursor) < 0));
        }
        // Failure details are never stored for display; the fixed message and issue code replace them.
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(PageSize + 1)
            .Select(x => new NotificationDto(x.Id, x.Version, x.Type, x.Severity, x.Severity == "error" ? "操作未完成" : x.Title,
                x.Severity == "error" ? Issues.Message(x.IssueCode) : x.Body, new(x.TargetKind, x.TargetId), x.CreatedAt, x.ReadAt, x.IssueCode))
            .ToListAsync(ct);
        return new NotificationPageDto(rows.Take(PageSize).ToArray(), count, rows.Count > PageSize);
    }
}
