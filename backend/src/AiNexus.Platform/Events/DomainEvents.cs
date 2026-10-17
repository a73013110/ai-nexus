using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Events;

/// <summary>領域事件標記介面：表示發布端模組內已發生的重要事件。請以過去式命名（名詞＋過去分詞），並宣告於引發該事件的程式碼旁。</summary>
public interface IDomainEvent;

/// <summary>
/// 跨模組之同交易反應處理常式。
/// 處理器於發布端的 SaveChanges 內部、在異動實際寫入資料庫前執行，共用同一個 DbContext 與資料庫交易：
/// 可追蹤實體、執行 ExecuteUpdate/ExecuteDelete 或引發後續領域事件，但自身絕不可呼叫 SaveChanges 或開啟/認可交易。
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent e, CancellationToken ct);
}

/// <summary>
/// Scoped 生命週期的領域事件收集與派送器。
/// <see cref="Raise"/> 僅登記事件；DbContext 於下次呼叫 SaveChangesAsync 開始時統一派送所有待處理事件，
/// 確保所有處理器的異動與發布端一同認可或復原。
/// </summary>
public sealed class DomainEvents(IServiceProvider services)
{
    /// <summary>處理器可引發連鎖事件；超過 8 輪視為遞迴錯誤而非正常工作負載。</summary>
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

    /// <summary>同步 SaveChanges 無法執行非同步處理常式；在此主動拋出例外防禦，避免副作用被無聲遺漏。</summary>
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
            // 失敗的儲存操作絕不可將殘留事件留給同一個 Scope 內後續無關的 SaveChanges 執行
            pending.Clear();
            throw;
        }
        finally { dispatching = false; }
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> Create(Type type)
        => DispatchMethod.MakeGenericMethod(type).CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task DispatchToHandlersAsync<TEvent>(IServiceProvider services, IDomainEvent e, CancellationToken ct) where TEvent : IDomainEvent
    {
        // 依照註冊順序執行；同一事件的多個處理器彼此之間不得依賴對方的執行副作用
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
