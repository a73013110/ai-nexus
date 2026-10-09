using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Audit;

public sealed record AuditDto(long Id, string Actor, string Action, Guid? ResourceId, string? Result, DateTimeOffset At, string? DetailsJson, string? ActingAs = null,
    string? Category = null, string? TraceId = null, Guid? OperationId = null, string? IssueCode = null);

/// <summary>
/// The newest 100 audit records matching the filters, before an optional cursor. Actor and subject accounts are joined
/// in; stored details are redacted again on the way out.
/// </summary>
internal sealed class ListActivityAudit(NexusDbContext db)
{
    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("", async (long? before, string? search, string? action, string? result, DateTimeOffset? from, DateTimeOffset? until, string? category, string? traceId, ListActivityAudit handler, CancellationToken ct) =>
            (await handler.HandleAsync(before, search, action, result, from, until, category, traceId, ct)).ToHttpResult())
        .WithName("ListAdminAudit").Produces<IReadOnlyList<AuditDto>>();

    public async Task<Result<IReadOnlyList<AuditDto>>> HandleAsync(long? before, string? search, string? action, string? result, DateTimeOffset? from, DateTimeOffset? until, string? category, string? traceId, CancellationToken ct)
    {
        if (search?.Length > 120 || action?.Length > 120 || result?.Length > 80 || before is <= 0 || from > until ||
            traceId is not null && (traceId.Length != 32 || !traceId.All(char.IsAsciiHexDigit)))
            return AuditErrors.InvalidFilter;
        if (!string.IsNullOrEmpty(category) && !AuditCategories.Values.Contains(category)) return AuditErrors.InvalidFilter;
        var query = from entry in AuditCategories.Filter(db.AuditEvents.AsNoTracking(), category)
                    join actorRecord in db.Users on (entry.ActorId ?? entry.OwnerId) equals actorRecord.Id into actors
                    from actor in actors.DefaultIfEmpty()
                    join subjectRecord in db.Users on entry.OwnerId equals subjectRecord.Id into subjects
                    from subject in subjects.DefaultIfEmpty()
                    select new { entry, actor, subject };
        if (before is { } cursor) query = query.Where(x => x.entry.Id < cursor);
        if (from is { } start) query = query.Where(x => x.entry.At >= start);
        if (until is { } end) query = query.Where(x => x.entry.At < end);
        if (traceId?.ToLowerInvariant() is { } trace) query = query.Where(x => x.entry.TraceId == trace);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.entry.Action.StartsWith(action));
        if (!string.IsNullOrWhiteSpace(result)) query = result == "failed"
            ? query.Where(x => x.entry.Result != null && !AuditOutcomes.Accepted.Contains(x.entry.Result))
            : result == "success" ? query.Where(x => x.entry.Result != null && AuditOutcomes.Accepted.Contains(x.entry.Result))
            : query.Where(x => x.entry.Result == result);
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (Guid.TryParse(search, out var resource)) query = query.Where(x => x.entry.ResourceId == resource || (x.actor != null && x.actor.Id == resource) || (x.subject != null && x.subject.Id == resource));
            else query = query.Where(x => (x.actor != null && (x.actor.Account.Contains(search) || x.actor.DisplayName.Contains(search))) || (x.subject != null && x.subject.Account.Contains(search)) || x.entry.Action.Contains(search) || x.entry.TraceId == search || x.entry.IssueCode == search || (x.entry.DetailsJson != null && x.entry.DetailsJson.Contains(search)));
        }
        var rows = await query.OrderByDescending(x => x.entry.Id).Take(100)
            .Select(x => new AuditDto(x.entry.Id, x.actor == null ? x.entry.Action == "identity.login" && x.entry.Result == "failed" ? "未驗證" : "system" : x.actor.Account, x.entry.Action, x.entry.ResourceId, x.entry.Result, x.entry.At, x.entry.DetailsJson, x.entry.ActorId != null && x.entry.ActorId != x.entry.OwnerId && x.subject != null ? x.subject.Account : null,
                null, x.entry.TraceId, x.entry.OperationId, x.entry.IssueCode)).ToListAsync(ct);
        return Result<IReadOnlyList<AuditDto>>.Ok(rows.Select(row => row with { Category = AuditCategories.For(row.Action), DetailsJson = AuditRedactor.Sanitize(row.DetailsJson, historical: true) }).ToArray());
    }
}
