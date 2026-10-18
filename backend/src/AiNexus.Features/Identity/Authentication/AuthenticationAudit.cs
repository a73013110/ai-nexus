using AiNexus.Data.App;
using System.Security.Claims;
using System.Text.Json;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>
/// 驗證事件以獨立 scope（新的 DbContext）寫入，不受同一請求中失敗的寫入回滾影響，讓失敗紀錄一定留得下來。
/// </summary>
public sealed class AuthenticationAudit(IServiceScopeFactory scopes, IHttpContextAccessor accessor)
{
    public async Task WriteAsync(string action, Guid? userId, string? account, string? method, string result,
        CancellationToken ct, string? failureCode = null, string? issueCode = null)
    {
        var http = accessor.HttpContext;
        var actor = userId ?? Guid.Empty;
        // 只有登出時才以目前 cookie 的管理者身分當作 actor。登入嘗試的帳號尚未驗證，絕不能把被拒絕的登入歸屬到現有 cookie。
        if (action == "identity.logout" && Guid.TryParse(http?.User.FindFirstValue(SessionIdentity.ActorId), out var administrator))
            actor = administrator;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditEvents.Add(new()
        {
            OwnerId = userId ?? Guid.Empty,
            ActorId = actor,
            ResourceId = userId,
            Action = action,
            Result = result,
            IssueCode = issueCode,
            DetailsJson = JsonSerializer.Serialize(new
            {
                account = account is null ? null : DiagnosticRedactor.Text(account.Trim(), 240),
                authentication = method is "local" or "ad" or "windows" or "test" ? method : "unknown",
                clientAddress = http?.Connection.RemoteIpAddress?.ToString(),
                failureCode,
            }),
        });
        await db.SaveChangesAsync(ct);
    }
}
