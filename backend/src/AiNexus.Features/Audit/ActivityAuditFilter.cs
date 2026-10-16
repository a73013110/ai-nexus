using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Audit;

/// <summary>The audit filters shared by the list and the export; paging is the list's own.</summary>
public sealed class ActivityAuditFilter
{
    /// <summary>An account, display name, action, trace, issue code or detail text; a GUID matches the resource or either account.</summary>
    public string? Search { get; set; }
    /// <summary>A stored action or an action prefix (<c>admin.</c>).</summary>
    public string? Action { get; set; }
    /// <summary>Any of these outcomes. <c>success</c> and <c>failed</c> stand for every accepted and every other recorded outcome.</summary>
    public string[]? Result { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? Until { get; set; }
    public string? Category { get; set; }
    public string? TraceId { get; set; }

    internal bool IsValid() =>
        !(Search?.Length > 120 || Action?.Length > 120 || Result?.Length > 10 || Result?.Any(x => x is null || x.Length > 80) == true || From > Until ||
          TraceId is not null && (TraceId.Length != 32 || !TraceId.All(char.IsAsciiHexDigit)) ||
          !string.IsNullOrEmpty(Category) && !AuditCategories.Values.Contains(Category));

    /// <summary>Matching records, newest first, older than <paramref name="before"/> when given. Present each row with <see cref="Present"/>.</summary>
    internal IQueryable<AuditDto> Query(NexusDbContext db, long? before = null)
    {
        var query = from entry in AuditCategories.Filter(db.AuditEvents.AsNoTracking(), Category)
                    join actorRecord in db.Users on (entry.ActorId ?? entry.OwnerId) equals actorRecord.Id into actors
                    from actor in actors.DefaultIfEmpty()
                    join subjectRecord in db.Users on entry.OwnerId equals subjectRecord.Id into subjects
                    from subject in subjects.DefaultIfEmpty()
                    select new { entry, actor, subject };
        if (before is { } cursor) query = query.Where(x => x.entry.Id < cursor);
        if (From is { } start) query = query.Where(x => x.entry.At >= start);
        if (Until is { } end) query = query.Where(x => x.entry.At < end);
        if (TraceId?.ToLowerInvariant() is { } trace) query = query.Where(x => x.entry.TraceId == trace);
        if (!string.IsNullOrWhiteSpace(Action)) { var action = Action; query = query.Where(x => x.entry.Action.StartsWith(action)); }
        if (Result?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() is { Length: > 0 } results)
        {
            bool success = results.Contains("success"), failed = results.Contains("failed");
            var exact = results.Where(x => x is not ("success" or "failed")).ToArray();
            query = query.Where(x => x.entry.Result != null && (success && AuditOutcomes.Accepted.Contains(x.entry.Result)
                || failed && !AuditOutcomes.Accepted.Contains(x.entry.Result) || exact.Contains(x.entry.Result)));
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search;
            if (Guid.TryParse(search, out var resource)) query = query.Where(x => x.entry.ResourceId == resource || (x.actor != null && x.actor.Id == resource) || (x.subject != null && x.subject.Id == resource));
            else query = query.Where(x => (x.actor != null && (x.actor.Account.Contains(search) || x.actor.DisplayName.Contains(search))) || (x.subject != null && x.subject.Account.Contains(search)) || x.entry.Action.Contains(search) || x.entry.TraceId == search || x.entry.IssueCode == search || (x.entry.DetailsJson != null && x.entry.DetailsJson.Contains(search)));
        }
        return query.OrderByDescending(x => x.entry.Id)
            .Select(x => new AuditDto(x.entry.Id, x.actor == null ? x.entry.Action == "identity.login" && x.entry.Result == "failed" ? "未驗證" : "system" : x.actor.Account, x.entry.Action, x.entry.ResourceId, x.entry.Result, x.entry.At, x.entry.DetailsJson, x.entry.ActorId != null && x.entry.ActorId != x.entry.OwnerId && x.subject != null ? x.subject.Account : null,
                null, x.entry.TraceId, x.entry.OperationId, x.entry.IssueCode));
    }

    /// <summary>Classifies the record and redacts its stored details again on the way out.</summary>
    internal static AuditDto Present(AuditDto row) => row with { Category = AuditCategories.For(row.Action), DetailsJson = AuditRedactor.Sanitize(row.DetailsJson, historical: true) };
}
