using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Dispatches the scope's pending domain events at the start of SaveChangesAsync, so the handlers' writes share the
/// publisher's transaction. Handlers may run ExecuteUpdate/ExecuteDelete, which commit on their own outside a transaction,
/// so a save with pending events and no ambient transaction is re-run inside one.
/// </summary>
internal sealed class DomainEventInterceptor(DomainEvents events) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context!;
        if (events.HasPending && db.Database.CurrentTransaction is null)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var saved = await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InterceptionResult<int>.SuppressWithResult(saved);
        }
        await events.DispatchAsync(cancellationToken);
        return result;
    }

    /// <summary>Synchronous saves cannot run async handlers; they fail instead of silently dropping the side effects.</summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        events.ThrowIfPending();
        return result;
    }
}
