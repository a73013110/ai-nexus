using AiNexus.Platform.Events;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Monitoring;

/// <summary>A signed-out browser session stops counting as live presence at once instead of after its heartbeat expires.</summary>
internal sealed class LeaveSignedOutSession(RuntimeTraffic traffic) : IDomainEventHandler<UserSignedOut>
{
    public Task HandleAsync(UserSignedOut e, CancellationToken ct)
    {
        traffic.Leave(e.UserId, e.SessionId);
        return Task.CompletedTask;
    }
}
