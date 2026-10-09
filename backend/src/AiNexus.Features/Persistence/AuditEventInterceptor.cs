using AiNexus.Features.Identity.Sessions;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Completes and redacts every audit row about to be inserted, whichever module added it: details and free text are
/// sanitized, trace and operation ids come from the current activity, and the actor from the signed-in session.
/// </summary>
internal sealed class AuditEventInterceptor(IHttpContextAccessor? http) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Prepare(eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Prepare(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    private void Prepare(DbContext db)
    {
        var added = db.ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToList();
        if (added.Count == 0) return;
        var actor = Guid.TryParse(http?.HttpContext?.User.FindFirst(SessionIdentity.ActorId)?.Value, out var id) ? id : (Guid?)null;
        foreach (var audit in added)
        {
            audit.DetailsJson = AuditRedactor.Sanitize(audit.DetailsJson);
            audit.Action = DiagnosticRedactor.Text(audit.Action, 64);
            audit.Result = audit.Result is null ? null : DiagnosticRedactor.Text(audit.Result, 80);
            audit.TraceId ??= System.Diagnostics.Activity.Current?.TraceId.ToHexString();
            audit.OperationId ??= Guid.TryParse(System.Diagnostics.Activity.Current?.GetTagItem("operation.id")?.ToString(), out var operation) ? operation : null;
            audit.ActorId ??= actor;
        }
    }
}
