using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

// Payload version and typed targets are independent of delivery channels and UI URLs.
public sealed class WorkspaceNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public int Version { get; set; } = 1;
    public string EventKey { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string? IssueCode { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string TargetKind { get; set; } = "";
    public Guid TargetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
}
public sealed record NotificationTargetDto(string Kind, Guid Id);
public sealed record NotificationDto(Guid Id, int Version, string Type, string Severity, string Title, string Body,
    NotificationTargetDto Target, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, string? IssueCode = null);
public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Unread, bool HasMore);

public sealed class NotificationService(NexusDbContext db)
{
    /// <summary>Caller saves the event with its source change in the same transaction.</summary>
    public async Task PublishAsync(Guid owner, string key, string type, string severity, string title, string body, string target, Guid id, CancellationToken ct, string? issueCode = null)
    {
        if (target is not ("conversation" or "task" or "share" or "repository-review") || severity is not ("info" or "success" or "error"))
            throw new InvalidOperationException("Unknown notification target or severity.");
        if (db.Set<WorkspaceNotification>().Local.Any(x => x.OwnerId == owner && x.EventKey == key)
            || await db.Set<WorkspaceNotification>().AnyAsync(x => x.OwnerId == owner && x.EventKey == key, ct)) return;
        db.Add(new WorkspaceNotification { OwnerId = owner, EventKey = key, Type = type, Severity = severity,
            Title = severity == "error" ? "操作未完成" : string.Concat(title.Take(180)), Body = severity == "error" ? Issues.Message(issueCode) : string.Concat(body.Take(600)), IssueCode = issueCode, TargetKind = target, TargetId = id });
    }
    public async Task<NotificationPageDto> ListAsync(Guid owner, bool unread, Guid? before, CancellationToken ct)
    {
        var all = db.Set<WorkspaceNotification>().AsNoTracking().Where(x => x.OwnerId == owner && x.DismissedAt == null);
        var count = await all.CountAsync(x => x.ReadAt == null, ct);
        var query = all.Where(x => !unread || x.ReadAt == null);
        if (before is Guid cursor) {
            var previous = await all.SingleOrDefaultAsync(x => x.Id == cursor, ct);
            if (previous is null) throw new ApiException(400, "notification_cursor_invalid", "通知清單已變更，請重新整理。");
            query = query.Where(x => x.CreatedAt < previous.CreatedAt || (x.CreatedAt == previous.CreatedAt && x.Id.CompareTo(cursor) < 0));
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(51)
            .Select(x => new NotificationDto(x.Id, x.Version, x.Type, x.Severity, x.Severity == "error" ? "操作未完成" : x.Title, x.Severity == "error" ? Issues.Message(x.IssueCode) : x.Body, new(x.TargetKind, x.TargetId), x.CreatedAt, x.ReadAt, x.IssueCode)).ToListAsync(ct);
        return new(rows.Take(50).ToArray(), count, rows.Count > 50);
    }
    public async Task ReadAsync(Guid owner, Guid? id, DateTimeOffset? through, CancellationToken ct)
    {
        var query = db.Set<WorkspaceNotification>().Where(x => x.OwnerId == owner && x.ReadAt == null && x.DismissedAt == null);
        if (id is Guid value) query = query.Where(x => x.Id == value);
        else {
            if (through is null) throw new ApiException(400, "notification_cursor_required", "請提供本次通知清單的時間。");
            query = query.Where(x => x.CreatedAt <= through);
        }
        await query.ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, DateTimeOffset.UtcNow), ct);
    }
    public Task DismissAsync(Guid owner, Guid id, CancellationToken ct) => db.Set<WorkspaceNotification>()
        .Where(x => x.Id == id && x.OwnerId == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.DismissedAt, DateTimeOffset.UtcNow), ct);
}
public sealed record ReadNotificationsRequest(DateTimeOffset Through);
public static class NotificationEndpoints
{
    public static void MapNotifications(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/notifications").WithTags("Notifications");
        routes.MapGet("", async (bool? unread, Guid? before, CurrentUser user, NotificationService service, CancellationToken ct) =>
            await service.ListAsync((await user.GetAsync(ct)).Id, unread ?? false, before, ct)).WithName("ListNotifications").Produces<NotificationPageDto>();
        routes.MapPost("/read", async (ReadNotificationsRequest body, CurrentUser user, NotificationService service, CancellationToken ct) =>
        { await service.ReadAsync((await user.GetAsync(ct)).Id, null, body.Through, ct); return Results.NoContent(); }).WithName("ReadNotifications");
        routes.MapPost("/{id:guid}/read", async (Guid id, CurrentUser user, NotificationService service, CancellationToken ct) =>
        { await service.ReadAsync((await user.GetAsync(ct)).Id, id, null, ct); return Results.NoContent(); }).WithName("ReadNotification");
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser user, NotificationService service, CancellationToken ct) =>
        { await service.DismissAsync((await user.GetAsync(ct)).Id, id, ct); return Results.NoContent(); }).WithName("DismissNotification");
    }
}
public static class NotificationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var n = model.Entity<WorkspaceNotification>(); n.ToTable("Notifications", "operations"); n.HasKey(x => x.Id);
        n.Property(x => x.IssueCode).HasMaxLength(40); n.Property(x => x.EventKey).HasMaxLength(160); n.Property(x => x.Type).HasMaxLength(80); n.Property(x => x.Severity).HasMaxLength(16);
        n.Property(x => x.Title).HasMaxLength(180); n.Property(x => x.Body).HasMaxLength(600); n.Property(x => x.TargetKind).HasMaxLength(32);
        n.HasIndex(x => new { x.OwnerId, x.EventKey }).IsUnique(); n.HasIndex(x => new { x.OwnerId, x.DismissedAt, x.ReadAt, x.CreatedAt });
        n.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
