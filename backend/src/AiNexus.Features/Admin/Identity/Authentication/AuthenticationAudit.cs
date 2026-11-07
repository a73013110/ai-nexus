using AiNexus.Data.App;
using AiNexus.Platform.Modules;
using System.Security.Claims;
using AiNexus.Data.App.Audit;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Admin.Identity.Sessions;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>
/// 登入與登出的稽核。以獨立 scope（新的 DbContext）寫入，不受同一請求中失敗的寫入回滾影響，讓失敗紀錄一定留得下來。
/// 登入端點本身宣告 NotAudited：請求稽核要先有身分，登入失敗時還沒有。
/// </summary>
[Scoped]
public sealed class AuthenticationAudit(IServiceScopeFactory scopes, IHttpContextAccessor accessor)
{
    /// <summary>
    /// <paramref name="failureCode"/> 為 null 是成功；失敗時 <paramref name="userId"/> 為 null（帳號未驗證，擁有者記空識別碼）。
    /// <paramref name="resource"/> 是稽核對象，預設為使用者本人；外部系統的帳密驗證記呼叫的系統，才查得到每個系統的呼叫紀錄。
    /// <paramref name="client"/> 是該系統當下的名稱：紀錄自己看得出是誰呼叫，系統改名或刪除後舊紀錄仍保留當時的名稱。
    /// </summary>
    public async Task WriteAsync(AuditAction action, Guid? userId, string? account, string? method, CancellationToken ct,
        string? failureCode = null, string? issueCode = null, Guid? resource = null, string? client = null)
    {
        var http = accessor.HttpContext;
        var actor = userId ?? Guid.Empty;
        // 只有登出時才以目前 cookie 的管理者身分當作 actor。登入嘗試的帳號尚未驗證，絕不能把被拒絕的登入歸屬到現有 cookie。
        if (action == IdentityAudit.SignedOut && Guid.TryParse(http?.User.FindFirstValue(SessionIdentity.ActorId), out var administrator))
            actor = administrator;
        await using var scope = scopes.CreateAsyncScope();
        var name = account is null ? null : DiagnosticRedactor.Text(account.Trim(), 240);
        var authentication = method is "local" or "ad" or "windows" or "test" ? method : "unknown";
        // 只有外部系統的呼叫才帶 client：網站登入的紀錄不多一個永遠是 null 的欄位。
        object details = client is null
            ? new { account = name, authentication }
            : new { account = name, authentication, client = DiagnosticRedactor.Text(client, 240) };
        var entry = scope.ServiceProvider.GetRequiredService<AuditTrail>().Record(action, userId ?? Guid.Empty, resource ?? userId, userId, details,
            failureCode ?? AuditEvent.Completed);
        entry.ActorId = actor;
        entry.IssueCode = issueCode;
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(ct);
    }
}
