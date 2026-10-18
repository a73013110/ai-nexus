using AiNexus.Features.Identity.Sessions;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiNexus.Features.Persistence;

/// <summary>在稽核事件寫入前完成脫敏、長度截斷與追蹤資訊補齊。</summary>
/// <remarks>
/// 脫敏必須發生在寫入資料庫之前，之後無法再補救。Action 與 Result 會截斷至欄位長度上限，
/// 否則 SQL 會直接寫入失敗。TraceId、OperationId、ActorId 只在尚未指定時補值，呼叫端明確指定的值優先。
/// ActorId 只能從已驗證的 HTTP 請求取得；背景工作沒有 HttpContext，會維持 null。
/// </remarks>
internal sealed class AuditEventInterceptor(IHttpContextAccessor? http) : SaveChangesInterceptor
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

        var actor = Guid.TryParse(http?.HttpContext?.User.FindFirst(SessionIdentity.ActorId)?.Value, out var id)
            ? id
            : (Guid?)null;

        foreach (var audit in added)
        {
            audit.DetailsJson = AuditRedactor.Sanitize(audit.DetailsJson);
            audit.Action = DiagnosticRedactor.Text(audit.Action, 64);
            audit.Result = audit.Result is null ? null : DiagnosticRedactor.Text(audit.Result, 80);

            // 追蹤識別取自目前的 Activity；沒有 Activity（例如測試或非請求情境）時維持 null。
            audit.TraceId ??= System.Diagnostics.Activity.Current?.TraceId.ToHexString();
            var operationTag = System.Diagnostics.Activity.Current?.GetTagItem("operation.id")?.ToString();
            audit.OperationId ??= Guid.TryParse(operationTag, out var operation) ? operation : null;

            audit.ActorId ??= actor;
        }
    }
}
