using System.Collections.Concurrent;
using System.Reflection;
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
public sealed class DomainEvents(IServiceProvider services)
{
    /// <summary>處理器可能連鎖引發事件；超過 8 輪就視為遞迴錯誤，而不是正常的工作量。</summary>
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

    /// <summary>同步 SaveChanges 無法執行非同步的處理常式，因此這裡主動拋出例外，避免副作用被悄悄漏掉。</summary>
    public void ThrowIfPending()
    {
        if (dispatching) throw new InvalidOperationException("Domain event handlers must not save changes; the publisher's SaveChanges persists their work.");
        if (pending.Count > 0)
        {
            pending.Clear();
            throw new InvalidOperationException("Pending domain events require SaveChangesAsync.");
        }
    }

    /// <summary>持續執行處理器直到沒有新事件引發為止。由 DbContext 在寫入自身變更前呼叫。</summary>
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
            // 儲存失敗時必須清空殘留事件，否則同一個 Scope 之後無關的 SaveChanges 會把它們派送出去。
            pending.Clear();
            throw;
        }
        finally { dispatching = false; }
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> Create(Type type)
        => DispatchMethod.MakeGenericMethod(type).CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task DispatchToHandlersAsync<TEvent>(IServiceProvider services, IDomainEvent e, CancellationToken ct) where TEvent : IDomainEvent
    {
        // 依註冊順序執行。同一事件的多個處理器之間不可依賴彼此的副作用。
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

    /// <summary>在訂閱端模組的 AddServices 中註冊事件處理器。</summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent where THandler : class, IDomainEventHandler<TEvent>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainEventHandler<TEvent>, THandler>());
        return services;
    }
}
