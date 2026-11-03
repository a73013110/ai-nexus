using AiNexus.Platform.Modules;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Events;

/// <summary>領域事件的標記介面，代表發布端模組內已發生的重要事件。事件名稱用過去式（名詞＋過去分詞），並宣告在引發它的程式碼旁。</summary>
public interface IDomainEvent;

/// <summary>
/// 跨模組的同交易處理常式。處理器在發布端的 SaveChanges 內、寫入資料庫之前執行，與發布端共用同一個 DbContext 與交易。
/// 可以追蹤實體、執行 ExecuteUpdate／ExecuteDelete，或再引發後續事件；但絕不可自行呼叫 SaveChanges，也不可開始或認可交易。
/// </summary>
[RegisterImplementations(typeof(DomainEventServices), nameof(DomainEventServices.AddDomainEventHandler))]
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

    private readonly List<(IDomainEvent Event, Dispatch Dispatch)> pending = [];
    private bool dispatching;

    public bool HasPending => pending.Count > 0;

    /// <summary>
    /// 登記事件。事件型別在這裡就已知，連同該型別的派送一起記下，派送時不必依執行期型別查表或以反射建立泛型。
    /// 處理器依 <typeparamref name="TEvent"/> 解析（DI 不支援泛型變異），所以必須以具體事件型別引發；以介面或基底型別傳入會找不到處理器，直接拒絕。
    /// </summary>
    public void Raise<TEvent>(TEvent e) where TEvent : class, IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.GetType() != typeof(TEvent))
            throw new ArgumentException($"Raise domain events with their concrete type: {e.GetType().FullName} was raised as {typeof(TEvent).FullName}.", nameof(e));
        pending.Add((e, Handlers<TEvent>.Dispatch));
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
                foreach (var (e, dispatch) in batch) await dispatch(services, e, ct);
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

    private delegate Task Dispatch(IServiceProvider services, IDomainEvent e, CancellationToken ct);

    // 每種事件一個委派，在型別第一次使用時建立；沒有處理器的事件解析出空集合，直接略過。
    private static class Handlers<TEvent> where TEvent : class, IDomainEvent
    {
        public static readonly Dispatch Dispatch = async (services, e, ct) =>
        {
            var typed = (TEvent)e;
            // 依註冊順序執行。同一事件的多個處理器之間不可依賴彼此的副作用。
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
                await handler.HandleAsync(typed, ct);
        };
    }
}

public static class DomainEventServices
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        services.TryAddScoped<DomainEvents>();
        return services;
    }

    /// <summary>註冊事件處理器（scoped，與發布端共用 DbContext）；同一個處理器不重複註冊。Features 的實作由組合產生器呼叫，不必手寫。</summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent where THandler : class, IDomainEventHandler<TEvent>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainEventHandler<TEvent>, THandler>());
        return services;
    }
}
