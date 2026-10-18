using System.Diagnostics;
using AiNexus.Platform.Security;

namespace AiNexus.Platform.Diagnostics;

/// <summary>一筆稽核的情境：有效身分、實際操作者、追蹤識別與時間（ADR 0014）。請求外（背景工作）的有效身分由呼叫端指定，否則為 null。</summary>
public sealed record AuditContext(Guid? OwnerId, Guid? ActorId, string? TraceId, Guid? OperationId, DateTimeOffset At);

/// <summary>
/// 稽核情境的唯一來源。EF 寫入（<c>AuditEventInterceptor</c>）與 procedure 寫入（session context）都從這裡取值，
/// 兩邊寫出的稽核才無法區分。
/// </summary>
/// <remarks>
/// ActorId 只取自已驗證請求的 claim；有效身分取自請求中已解析的使用者（<see cref="IRequestUser"/>），尚未解析時為 null，
/// procedure 寫稽核會因 <c>OwnerId</c> NOT NULL 失敗，不會留下沒有身分的異動。
/// </remarks>
public sealed class AuditContextSource(TimeProvider clock, IHttpContextAccessor? http = null)
{
    public AuditContext Capture(Guid? owner = null)
    {
        var request = http?.HttpContext;
        var activity = Activity.Current;
        return new(
            owner ?? request?.RequestServices.GetService<IRequestUser>()?.ResolvedId,
            Guid.TryParse(request?.User.FindFirst(RequestClaims.ActorId)?.Value, out var actor) ? actor : null,
            activity?.TraceId.ToHexString(),
            Guid.TryParse(activity?.GetTagItem("operation.id")?.ToString(), out var operation) ? operation : null,
            clock.GetUtcNow());
    }
}
