using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiNexus.Platform.Data;

/// <summary>
/// 領域事件分發攔截器。
/// </summary>
/// <remarks>
/// 事件處理程式可能含有自行提交的指令（例如 <c>ExecuteUpdateAsync</c>）。若呼叫端沒有開啟事務，這裡會自行開一個事務，
/// 把 SaveChanges 與事件分發包在一起，確保同進同退。
/// 同步的 SaveChanges 無法執行非同步分發，因此有待處理事件時直接拋出例外，而不是靜默漏掉副作用。
/// </remarks>
public sealed class DomainEventInterceptor(DomainEvents events) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// 同步 SaveChanges 無法執行異步領域事件分發；若有待處理事件則立即拋出異常，防止靜默遺漏領域副作用。
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        events.ThrowIfPending();
        return result;
    }
}
