using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Monitoring.Traffic;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Monitoring.Presence;

/// <summary>使用者登出時，該瀏覽器工作階段立即不再計為即時在線，不必等心跳逾時。</summary>
internal sealed class LeaveSignedOutSession(RuntimeTraffic traffic) : IDomainEventHandler<UserSignedOut>
{
    public Task HandleAsync(UserSignedOut e, CancellationToken ct)
    {
        traffic.Leave(e.UserId, e.SessionId);
        return Task.CompletedTask;
    }
}
