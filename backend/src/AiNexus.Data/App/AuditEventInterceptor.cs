using AiNexus.Data.App.Audit;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiNexus.Data.App;

/// <summary>在稽核事件寫入前完成脫敏、長度截斷與追蹤資訊補齊。</summary>
/// <remarks>
/// 脫敏必須發生在寫入資料庫之前，之後無法再補救。Action 與 Result 會截斷至欄位長度上限，
/// 否則 SQL 會直接寫入失敗。TraceId、OperationId、ActorId 只在尚未指定時補值，呼叫端明確指定的值優先；
/// 值取自 <see cref="AuditContextSource"/>，與 procedure 的 session context 同一個來源（ADR 0014）。
/// </remarks>
internal sealed class AuditEventInterceptor(AuditContextSource source) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Prepare(eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Prepare(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    private void Prepare(DbContext db)
    {
        var added = db.ChangeTracker.Entries<AuditEvent>()
            .Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity)
            .ToList();

        if (added.Count == 0)
        {
            return;
        }

        // OwnerId 與 At 由呼叫端與 CreationTime 決定，這裡只補追蹤與實際操作者。
        var context = source.Capture();
        foreach (var audit in added)
        {
            audit.DetailsJson = AuditRedactor.Sanitize(audit.DetailsJson);
            audit.Action = DiagnosticRedactor.Text(audit.Action, 64);
            audit.Result = audit.Result is null ? null : DiagnosticRedactor.Text(audit.Result, 80);
            audit.TraceId ??= context.TraceId;
            audit.OperationId ??= context.OperationId;
            audit.ActorId ??= context.ActorId;
        }
    }
}
