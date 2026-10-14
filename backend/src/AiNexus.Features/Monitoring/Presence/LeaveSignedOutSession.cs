using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Monitoring.Traffic;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Monitoring.Presence;

/// <summary>A signed-out browser session stops counting as live presence at once instead of after its heartbeat expires.</summary>
internal sealed class LeaveSignedOutSession(RuntimeTraffic traffic) : IDomainEventHandler<UserSignedOut>
{
    public Task HandleAsync(UserSignedOut e, CancellationToken ct)
    {
        traffic.Leave(e.UserId, e.SessionId);
        return Task.CompletedTask;
    }
}
