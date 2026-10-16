using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Audit;

public sealed record AuditDto(long Id, string Actor, string Action, Guid? ResourceId, string? Result, DateTimeOffset At, string? DetailsJson, string? ActingAs = null,
    string? Category = null, string? TraceId = null, Guid? OperationId = null, string? IssueCode = null);

/// <summary>
/// The newest 25, 50 or 100 (default) audit records matching the filters, before an optional cursor. Actor and subject
/// accounts are joined in; stored details are redacted again on the way out.
/// </summary>
internal sealed class ListActivityAudit(NexusDbContext db)
{
    public static readonly int[] PageSizes = [25, 50, 100];

    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("", async ([AsParameters] ActivityAuditFilter filter, long? before, int? take, ListActivityAudit handler, CancellationToken ct) =>
            (await handler.HandleAsync(filter, before, take, ct)).ToHttpResult())
        .WithName("ListAdminAudit");

    public async Task<Result<IReadOnlyList<AuditDto>>> HandleAsync(ActivityAuditFilter filter, long? before, int? take, CancellationToken ct)
    {
        if (!filter.IsValid() || before is <= 0 || take is { } size && !PageSizes.Contains(size)) return AuditErrors.InvalidFilter;
        var rows = await filter.Query(db, before).Take(take ?? 100).ToListAsync(ct);
        return Result<IReadOnlyList<AuditDto>>.Ok(rows.Select(ActivityAuditFilter.Present).ToArray());
    }
}
