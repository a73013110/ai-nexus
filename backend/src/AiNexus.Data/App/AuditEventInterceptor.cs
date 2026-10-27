using AiNexus.Data.App.Audit;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiNexus.Data.App;

/// <summary>在稽核事件寫入前完成脫敏、長度截斷與追蹤資訊補齊。</summary>
/// <remarks>
/// 脫敏必須發生在寫入資料庫之前，之後無法再補救。Action 與 Result 會截斷至欄位長度上限，否則 SQL 會直接寫入失敗。
/// 追蹤、實際操作者、功能與來源 IP 只在尚未指定時補值，呼叫端明確指定的值優先；值取自 <see cref="AuditContextSource"/>。
/// 沒有分類的稽核代表繞過了 <see cref="AuditTrail"/>（直接 new <see cref="AuditEvent"/>），直接拒絕。
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
        var changed = db.ChangeTracker.Entries<AuditEvent>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => x.Entity)
            .ToList();

        if (changed.Count == 0)
        {
            return;
        }

        // OwnerId 與 At 由 AuditTrail 與 CreationTime 決定，這裡只補請求情境。
        var context = source.Capture();
        foreach (var audit in changed)
        {
            if (string.IsNullOrEmpty(audit.Category)) throw new InvalidOperationException($"稽核 {audit.Action} 沒有分類：請經 AuditTrail 寫入。");
            audit.DetailsJson = AuditRedactor.Sanitize(audit.DetailsJson);
            audit.Action = DiagnosticRedactor.Text(audit.Action, AuditEvent.ActionMaxLength);
            audit.Result = DiagnosticRedactor.Text(audit.Result, AuditEvent.ResultMaxLength);
            audit.TraceId ??= context.TraceId;
            audit.OperationId ??= context.OperationId;
            audit.ActorId ??= context.ActorId;
            audit.FeatureId ??= context.FeatureId;
            audit.ClientAddress ??= context.ClientAddress;
        }
    }
}
