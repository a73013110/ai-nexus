using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Events;

/// <summary>領域事件的標記介面，代表發布端模組內已發生的重要事件。事件名稱用過去式（名詞＋過去分詞），並宣告在引發它的程式碼旁。</summary>
public interface IDomainEvent;

/// <summary>
/// 跨模組的同交易處理常式。處理器在發布端的 SaveChanges 內、寫入資料庫之前執行，與發布端共用同一個 DbContext 與交易。
/// 可以追蹤實體、執行 ExecuteUpdate／ExecuteDelete，或再引發後續事件；但絕不可自行呼叫 SaveChanges，也不可開始或認可交易。
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent e, CancellationToken ct);
}

/// <summary>
/// Scoped 的領域事件收集與派送器。<see cref="Raise"/> 只登記事件，DbContext 在下一次 SaveChangesAsync 開始時統一派送，
/// 讓所有處理器的異動與發布端一起認可或復原。
/// </summary>
public sealed class DomainEvents(IServiceProvider services, DomainEventDispatchers dispatchers)
{
    /// <summary>處理器可能連鎖引發事件；超過 8 輪就視為遞迴錯誤，而不是正常的工作量。</summary>
    public const int MaxRounds = 8;

    private readonly List<IDomainEvent> pending = [];
    private bool dispatching;

    public bool HasPending => pending.Count > 0;

    public void Raise(IDomainEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        pending.Add(e);
    }

    /// <summary>同步 SaveChanges 無法執行非同步的處理常式，因此這裡主動拋出例外，避免副作用被悄悄漏掉。</summary>
    public void ThrowIfPending()
    {
        if (dispatching) throw NestedSave();
        if (pending.Count > 0)
        {
            pending.Clear();
            throw new InvalidOperationException("Pending domain events require SaveChangesAsync.");
        }
    }

    /// <summary>持續執行處理器直到沒有新事件引發為止。由 DbContext 在寫入自身變更前呼叫。</summary>
    public async Task DispatchAsync(CancellationToken ct)
    {
        if (dispatching) throw NestedSave();
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
                    if (dispatchers.Find(e.GetType()) is { } dispatcher) await dispatcher.DispatchAsync(services, e, ct);
            }
        }
        catch
        {
            // 儲存失敗時必須清空殘留事件，否則同一個 Scope 之後無關的 SaveChanges 會把它們派送出去。
            pending.Clear();
            throw;
        }
        finally { dispatching = false; }
    }

    // 處理器在派送中又呼叫 SaveChanges 會重入攔截器；同步與非同步路徑都以同一訊息拒絕。
    private static InvalidOperationException NestedSave() => new("Domain event handlers must not save changes; the publisher's SaveChanges persists their work.");
}

/// <summary>
/// 一種事件的派送器。註冊處理器時就知道事件型別，在那裡建立強型別的派送器，派送時依事件的執行期型別查表，不必以反射建立泛型方法。
/// </summary>
internal interface IDomainEventDispatcher
{
    Type EventType { get; }

    Task DispatchAsync(IServiceProvider services, IDomainEvent e, CancellationToken ct);
}

internal sealed class DomainEventDispatcher<TEvent> : IDomainEventDispatcher where TEvent : IDomainEvent
{
    public Type EventType => typeof(TEvent);

    public async Task DispatchAsync(IServiceProvider services, IDomainEvent e, CancellationToken ct)
    {
        // 依註冊順序執行。同一事件的多個處理器之間不可依賴彼此的副作用。
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync((TEvent)e, ct);
    }
}

/// <summary>
/// 事件型別到派送器的查詢表。只比對事件的確切型別（DI 不支援泛型變異）；沒有任何處理器的事件查不到，直接略過。
/// </summary>
public sealed class DomainEventDispatchers
{
    private readonly FrozenDictionary<Type, IDomainEventDispatcher> byType;

    // 每種事件只有一個派送器：AddDomainEventHandler 以 TryAddEnumerable 註冊，同一個實作型別不會重複。
    internal DomainEventDispatchers(IEnumerable<IDomainEventDispatcher> dispatchers) => byType = dispatchers.ToFrozenDictionary(x => x.EventType);

    internal IDomainEventDispatcher? Find(Type eventType) => byType.GetValueOrDefault(eventType);
}

public static class DomainEventServices
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        services.TryAddScoped<DomainEvents>();
        services.TryAddSingleton(provider => new DomainEventDispatchers(provider.GetServices<IDomainEventDispatcher>()));
        return services;
    }

    /// <summary>在訂閱端模組的 AddServices 中註冊事件處理器，同時登記該事件的派送器。</summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent where THandler : class, IDomainEventHandler<TEvent>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainEventHandler<TEvent>, THandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDomainEventDispatcher, DomainEventDispatcher<TEvent>>());
        return services;
    }

    /// <summary>以工廠註冊處理器（需要額外狀態時，例如測試）。直接註冊 <see cref="IDomainEventHandler{TEvent}"/> 不會建立派送器，事件不會送到它。</summary>
    public static IServiceCollection AddDomainEventHandler<TEvent>(this IServiceCollection services, Func<IServiceProvider, IDomainEventHandler<TEvent>> factory)
        where TEvent : IDomainEvent
    {
        services.AddScoped(factory);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDomainEventDispatcher, DomainEventDispatcher<TEvent>>());
        return services;
    }
}
