using System.Security.Claims;
using System.Text.Json;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Identity;

/// <summary>Durable authentication events use a fresh context, independent of failed credential writes.</summary>
public sealed class AuthenticationAudit(IServiceScopeFactory scopes, IHttpContextAccessor accessor)
{
    public async Task WriteAsync(string action, Guid? userId, string? account, string? method, string result,
        CancellationToken ct, string? failureCode = null, string? issueCode = null)
    {
        var http = accessor.HttpContext;
        var actor = userId ?? Guid.Empty;
        // An attempted account is unverified; never attribute a rejected login to the current cookie.
        if (action == "identity.logout" && Guid.TryParse(http?.User.FindFirstValue(SessionIdentity.ActorId), out var administrator)) actor = administrator;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.AuditEvents.Add(new()
        {
            OwnerId = userId ?? Guid.Empty, ActorId = actor, ResourceId = userId,
            Action = action, Result = result, IssueCode = issueCode,
            DetailsJson = JsonSerializer.Serialize(new
            {
                account = account is null ? null : DiagnosticRedactor.Text(account.Trim(), 240), authentication = method is "local" or "ad" or "windows" or "test" ? method : "unknown",
                clientAddress = http?.Connection.RemoteIpAddress?.ToString(), failureCode,
            }),
        });
        await db.SaveChangesAsync(ct);
    }
}
