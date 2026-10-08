using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Events;

/// <summary>Something that happened in the publishing module. Declare events as past-tense records next to the code that raises them.</summary>
public interface IDomainEvent;

/// <summary>
/// Same-transaction reaction of another module. Handlers run inside the publisher's SaveChanges, before its own changes
/// are written, on the same DbContext and transaction: they may track entities, run ExecuteUpdate/ExecuteDelete and raise
/// further events, but must not call SaveChanges or begin/commit transactions themselves.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent e, CancellationToken ct);
}

/// <summary>
/// Scoped collector. <see cref="Raise"/> only records the event; the DbContext dispatches every pending event at the start
/// of its next SaveChangesAsync, so the handlers commit or roll back together with the publisher.
/// </summary>
public sealed class DomainEvents(IServiceProvider services)
{
    /// <summary>Handlers may raise further events; a chain longer than this is a bug, not a workload.</summary>
    public const int MaxRounds = 8;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Dispatchers = new();
    private static readonly MethodInfo DispatchMethod = typeof(DomainEvents).GetMethod(nameof(DispatchToHandlersAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly List<IDomainEvent> pending = [];
    private bool dispatching;

    public bool HasPending => pending.Count > 0;

    public void Raise(IDomainEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        pending.Add(e);
    }

    /// <summary>Synchronous saves cannot run async handlers; they fail instead of silently dropping the side effects.</summary>
    public void ThrowIfPending()
    {
        if (dispatching) throw new InvalidOperationException("Domain event handlers must not save changes; the publisher's SaveChanges persists their work.");
        if (pending.Count > 0)
        {
            pending.Clear();
            throw new InvalidOperationException("Pending domain events require SaveChangesAsync.");
        }
    }

    /// <summary>Runs handlers until no new events are raised. Called by the DbContext before it writes its own changes.</summary>
    public async Task DispatchAsync(CancellationToken ct)
    {
        if (dispatching) throw new InvalidOperationException("Domain event handlers must not save changes; the publisher's SaveChanges persists their work.");
        if (pending.Count == 0) return;
        dispatching = true;
        try
        {
            for (var round = 1; pending.Count > 0; round++)
            {
                if (round > MaxRounds) throw new InvalidOperationException($"Domain events were still being raised after {MaxRounds} rounds.");
                var batch = pending.ToArray();
                pending.Clear();
                foreach (var e in batch)
                    await Dispatchers.GetOrAdd(e.GetType(), Create)(services, e, ct);
            }
        }
        catch
        {
            // A failed save must not leave events behind for a later, unrelated SaveChanges in the same scope.
            pending.Clear();
            throw;
        }
        finally { dispatching = false; }
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> Create(Type type)
        => DispatchMethod.MakeGenericMethod(type).CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task DispatchToHandlersAsync<TEvent>(IServiceProvider services, IDomainEvent e, CancellationToken ct) where TEvent : IDomainEvent
    {
        // Registration order; handlers of one event must not depend on each other's effects.
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync((TEvent)e, ct);
    }
}

public static class DomainEventServices
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        services.TryAddScoped<DomainEvents>();
        return services;
    }

    /// <summary>Registers a subscriber in the subscribing module's AddServices.</summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent where THandler : class, IDomainEventHandler<TEvent>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainEventHandler<TEvent>, THandler>());
        return services;
    }
}
